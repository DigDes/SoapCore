using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Extensibility;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// GET {pattern}/api/services: what the explorer lists and how it describes each endpoint.
	[TestClass]
	public class SoapUiCatalogTests
	{
		private static readonly Dictionary<string, MessageVersion> VersionCodes = new Dictionary<string, MessageVersion>
		{
			{ "11", MessageVersion.Soap11 },
			{ "12", MessageVersion.Soap12WSAddressing10 },
			{ "12b", MessageVersion.Soap12WSAddressingAugust2004 },
			{ "none", MessageVersion.None },
		};

		[TestMethod]
		public async Task Services_RoutingMode_ListsEndpointsWithFullContract()
		{
			using var host = UiTestHost.CreateWithUi(UiTestHost.MapDefaultSoapEndpoints);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual("application/json", response.Content.Headers.ContentType.MediaType);
			Assert.AreEqual("utf-8", response.Content.Headers.ContentType.CharSet);

			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			var root = document.RootElement;

			CollectionAssert.AreEqual(
				new[] { "title", "description", "tryItOutEnabled", "operationDetailsEnabled", "basePath", "defaultSoapVersion", "docExpansion", "authSchemes", "apiKeyHeaderName", "stylesheetUrl", "formEditorEnabled", "sendTimeoutSeconds", "maxResponseBytes", "uiVersion", "services" },
				UiTestHost.PropertyNames(root));
			Assert.AreEqual("SoapCore Explorer", root.GetProperty("title").GetString());
			Assert.IsTrue(root.GetProperty("tryItOutEnabled").GetBoolean());
			Assert.IsFalse(root.GetProperty("operationDetailsEnabled").GetBoolean(), "Operation details are opt-in.");
			Assert.AreEqual(string.Empty, root.GetProperty("basePath").GetString());
			Assert.AreEqual(2, root.GetProperty("services").GetArrayLength());

			var svc = UiTestHost.GetService(document, 0);
			CollectionAssert.AreEqual(
				new[] { "id", "name", "path", "wsdlUrl", "serviceType", "serializer", "customSerializerType", "soapVersions", "caseInsensitivePath", "description", "operations", "operationsError" },
				UiTestHost.PropertyNames(svc));
			Assert.AreEqual("service-svc", svc.GetProperty("id").GetString());
			Assert.AreEqual("ITestService", svc.GetProperty("name").GetString());
			Assert.AreEqual("/Service.svc", svc.GetProperty("path").GetString());
			Assert.AreEqual("/Service.svc?wsdl", svc.GetProperty("wsdlUrl").GetString());
			Assert.AreEqual("SoapCore.Tests.TestService", svc.GetProperty("serviceType").GetString());
			Assert.AreEqual("DataContractSerializer", svc.GetProperty("serializer").GetString());
			Assert.AreEqual(JsonValueKind.Null, svc.GetProperty("customSerializerType").ValueKind);
			CollectionAssert.AreEqual(new[] { "1.1", "1.2" }, UiTestHost.Strings(svc.GetProperty("soapVersions")));
			Assert.IsFalse(svc.GetProperty("caseInsensitivePath").GetBoolean());
			Assert.AreEqual(JsonValueKind.Null, svc.GetProperty("operationsError").ValueKind);

			var asmx = UiTestHost.GetService(document, 1);
			Assert.AreEqual("service-asmx", asmx.GetProperty("id").GetString());
			Assert.AreEqual("/Service.asmx", asmx.GetProperty("path").GetString());
			Assert.AreEqual("XmlSerializer", asmx.GetProperty("serializer").GetString());
			CollectionAssert.AreEqual(new[] { "1.1" }, UiTestHost.Strings(asmx.GetProperty("soapVersions")));

			var ping = UiTestHost.GetOperation(svc, "Ping");
			CollectionAssert.AreEqual(
				new[] { "name", "soapAction", "isOneWay", "contract", "contractNamespace", "description", "parameters", "returns", "faults", "messageContract" },
				UiTestHost.PropertyNames(ping));
			Assert.AreEqual("http://tempuri.org/ITestService/Ping", ping.GetProperty("soapAction").GetString());
			Assert.IsFalse(ping.GetProperty("isOneWay").GetBoolean());
			Assert.AreEqual("ITestService", ping.GetProperty("contract").GetString());
			Assert.AreEqual("http://tempuri.org/", ping.GetProperty("contractNamespace").GetString());
		}

		[TestMethod]
		public async Task Services_MiddlewareMode_ListsEndpoint()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseSoapEndpoint<TestService>("/Middleware.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			Assert.AreEqual(1, document.RootElement.GetProperty("services").GetArrayLength());
			Assert.AreEqual("/Middleware.svc", svc.GetProperty("path").GetString());
			Assert.AreEqual("/Middleware.svc?wsdl", svc.GetProperty("wsdlUrl").GetString());
			Assert.AreEqual("SoapCore.Tests.TestService", svc.GetProperty("serviceType").GetString());
		}

		[TestMethod]
		public async Task Services_UiMappedBeforeSoapEndpoints_StillListsThem()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						endpoints.MapSoapCoreUi();
						UiTestHost.MapDefaultSoapEndpoints(endpoints);
					});
				});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			CollectionAssert.AreEqual(new[] { "service-svc", "service-asmx" }, UiTestHost.ServiceIds(document));
		}

		[TestMethod]
		public async Task Services_EndpointRegisteredAfterFirstRequest_AppearsInNextRequest()
		{
			using var host = UiTestHost.CreateWithUi(endpoints => endpoints.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer));

			using var before = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			CollectionAssert.AreEqual(new[] { "service-svc" }, UiTestHost.ServiceIds(before));

			UiTestHost.Register(host, "/Late.svc", typeof(UiTestService));

			using var after = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			CollectionAssert.AreEqual(new[] { "service-svc", "late-svc" }, UiTestHost.ServiceIds(after));
		}

		[TestMethod]
		public async Task Services_NoEndpoints_ReturnsEmptyList()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var services = document.RootElement.GetProperty("services");
			Assert.AreEqual(JsonValueKind.Array, services.ValueKind);
			Assert.AreEqual(0, services.GetArrayLength());
		}

		[TestMethod]
		[DataRow("11", "1.1")]
		[DataRow("12", "1.2")]
		[DataRow("11,12", "1.1,1.2")]
		[DataRow("12,11", "1.1,1.2")]
		[DataRow("12,12b,11", "1.1,1.2")]
		[DataRow("none", "")]
		public async Task SoapVersions_AreMappedDeduplicatedAndSorted(string codes, string expected)
		{
			using var host = UiTestHost.CreateWithUi(null);
			host.Services.GetRequiredService<ISoapEndpointRegistry>().Register(new SoapEndpointInfo
			{
				Path = "/V.svc",
				ServiceType = typeof(UiTestService),
				MessageVersions = codes.Split(',').Select(code => VersionCodes[code]).ToArray(),
			});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			Assert.AreEqual(expected, string.Join(",", UiTestHost.Strings(svc.GetProperty("soapVersions"))));
			Assert.AreEqual(3, svc.GetProperty("operations").GetArrayLength(), "The operations are listed whatever the message versions are.");
		}

		[TestMethod]
		public async Task Operations_ExposeNameSoapActionOneWayContractAndNamespace()
		{
			using var host = UiTestHost.CreateWithUi(endpoints => endpoints.UseSoapEndpoint<UiTestService>("/Ui.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			Assert.AreEqual("IUiTestService", svc.GetProperty("name").GetString());
			CollectionAssert.AreEqual(new[] { "CustomAction", "Echo", "Fire" }, UiTestHost.OperationNames(svc));

			var echo = UiTestHost.GetOperation(svc, "Echo");
			Assert.AreEqual("urn:ui-tests/IUiTestService/Echo", echo.GetProperty("soapAction").GetString());
			Assert.IsFalse(echo.GetProperty("isOneWay").GetBoolean());
			Assert.AreEqual("IUiTestService", echo.GetProperty("contract").GetString());
			Assert.AreEqual("urn:ui-tests", echo.GetProperty("contractNamespace").GetString());

			Assert.IsTrue(UiTestHost.GetOperation(svc, "Fire").GetProperty("isOneWay").GetBoolean());
			Assert.AreEqual("urn:ui-tests/CustomAction", UiTestHost.GetOperation(svc, "CustomAction").GetProperty("soapAction").GetString());
		}

		[TestMethod]
		public async Task Operations_WithGenerateSoapActionWithoutContractName_OmitContractName()
		{
			using var host = UiTestHost.CreateWithUi(endpoints => endpoints.UseSoapEndpoint(typeof(UiTestService), options =>
			{
				options.Path = "/Ui.svc";
				options.GenerateSoapActionWithoutContractName = true;
			}));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var echo = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Echo");
			Assert.AreEqual("urn:ui-tests/Echo", echo.GetProperty("soapAction").GetString());
		}

		[TestMethod]
		public async Task Operations_FollowSoapCoreNamingRulesAndAreUnique()
		{
			using var host = UiTestHost.CreateWithUi(endpoints => endpoints.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var names = UiTestHost.OperationNames(UiTestHost.GetService(document, 0));
			CollectionAssert.Contains(names, "OperationNameTest");
			CollectionAssert.Contains(names, "OverloadDouble");
			Assert.AreEqual(names.Length, names.Distinct(StringComparer.Ordinal).Count(), "Operation names must be unique.");
			CollectionAssert.AreEqual(names.OrderBy(name => name, StringComparer.Ordinal).ToArray(), names);
		}

		[TestMethod]
		public async Task Services_SamePathTwice_GetUniqueIds()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseWhen(
						context => context.Request.Headers.ContainsKey("X-Soap12"),
						branch => branch.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 }, SoapSerializer.DataContractSerializer));
					app.UseWhen(
						context => !context.Request.Headers.ContainsKey("X-Soap12"),
						branch => branch.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11 }, SoapSerializer.DataContractSerializer));
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(2, document.RootElement.GetProperty("services").GetArrayLength());
			CollectionAssert.AreEqual(new[] { "service-svc", "service-svc-2" }, UiTestHost.ServiceIds(document));
			Assert.AreEqual("/Service.svc", UiTestHost.GetService(document, 0).GetProperty("path").GetString());
			Assert.AreEqual("/Service.svc", UiTestHost.GetService(document, 1).GetProperty("path").GetString());
			CollectionAssert.AreEqual(new[] { "1.2" }, UiTestHost.Strings(UiTestHost.GetService(document, 0).GetProperty("soapVersions")));
			CollectionAssert.AreEqual(new[] { "1.1" }, UiTestHost.Strings(UiTestHost.GetService(document, 1).GetProperty("soapVersions")));
		}

		[TestMethod]
		public async Task Services_Ids_AreUrlSafeSlugsAndNeverCollide()
		{
			using var host = UiTestHost.CreateWithUi(null);
			UiTestHost.Register(host, "/a_b", typeof(UiTestService));
			UiTestHost.Register(host, "/a/b", typeof(UiTestService));
			UiTestHost.Register(host, "/", typeof(UiTestService));
			UiTestHost.Register(host, "/My Service.svc", typeof(UiTestService));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			CollectionAssert.AreEqual(new[] { "a-b", "a-b-2", "service", "my-service-svc" }, UiTestHost.ServiceIds(document));
		}

		[TestMethod]
		[DataRow("Service.svc", "/Service.svc")]
		[DataRow("/Service.svc/", "/Service.svc")]
		[DataRow("", "/")]
		public async Task Services_NonNormalizedPaths_AreNormalized(string registered, string expectedPath)
		{
			using var host = UiTestHost.CreateWithUi(null);
			UiTestHost.Register(host, registered, typeof(UiTestService));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			Assert.AreEqual(expectedPath, svc.GetProperty("path").GetString());
			Assert.AreEqual(expectedPath + "?wsdl", svc.GetProperty("wsdlUrl").GetString());
		}

		[TestMethod]
		public async Task PathBase_IsPrependedToPathAndWsdlUrl()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UsePathBase("/base");
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						UiTestHost.MapDefaultSoapEndpoints(endpoints);
						endpoints.MapSoapCoreUi();
					});
				});

			using var document = await UiTestHost.GetJsonAsync(host, "/base/soap-ui/api/services");

			Assert.AreEqual("/base", document.RootElement.GetProperty("basePath").GetString());
			var svc = UiTestHost.GetService(document, 0);
			Assert.AreEqual("/base/Service.svc", svc.GetProperty("path").GetString());
			Assert.AreEqual("/base/Service.svc?wsdl", svc.GetProperty("wsdlUrl").GetString());
		}

		[TestMethod]
		[DataRow("")]
		[DataRow("/base")]
		public async Task EndpointPathPrefix_IsInsertedBetweenPathBaseAndPath(string pathBase)
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(options => options.EndpointPathPrefix = "/soap"),
				app =>
				{
					if (!string.IsNullOrEmpty(pathBase))
					{
						app.UsePathBase(pathBase);
					}

					app.Map("/soap", branch => branch.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer));
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});

			using var document = await UiTestHost.GetJsonAsync(host, pathBase + UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			Assert.AreEqual(pathBase, document.RootElement.GetProperty("basePath").GetString());
			Assert.AreEqual(pathBase + "/soap/Service.svc", svc.GetProperty("path").GetString());
			Assert.AreEqual(pathBase + "/soap/Service.svc?wsdl", svc.GetProperty("wsdlUrl").GetString());
		}

		[TestMethod]
		public async Task EndpointPathPrefix_AppliesToAllEndpoints()
		{
			// Documents the global behaviour: one prefix for every endpoint, so it must match how all of them are hosted.
			using var host = UiTestHost.CreateWithUi(
				endpoints =>
				{
					endpoints.UseSoapEndpoint<TestService>("/Root.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
					endpoints.UseSoapEndpoint<TestService>("/Other.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				},
				options => options.EndpointPathPrefix = "/soap");

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual("/soap/Root.svc", UiTestHost.GetService(document, 0).GetProperty("path").GetString());
			Assert.AreEqual("/soap/Other.svc", UiTestHost.GetService(document, 1).GetProperty("path").GetString());
		}

		[TestMethod]
		public async Task OperationsError_IsIsolatedPerService()
		{
			using var host = UiTestHost.CreateWithUi(endpoints => endpoints.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer));
			UiTestHost.Register(host, "/Broken.svc", null);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);
			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

			var good = UiTestHost.GetService(document, 0);
			Assert.AreEqual(JsonValueKind.Null, good.GetProperty("operationsError").ValueKind);
			Assert.IsTrue(good.GetProperty("operations").GetArrayLength() > 0);

			var broken = UiTestHost.GetService(document, 1);
			Assert.AreEqual("/Broken.svc", broken.GetProperty("name").GetString());
			Assert.AreEqual("/Broken.svc", broken.GetProperty("path").GetString());
			Assert.AreEqual(string.Empty, broken.GetProperty("serviceType").GetString());
			Assert.AreEqual("The endpoint has no service type.", broken.GetProperty("operationsError").GetString());
			Assert.AreEqual(0, broken.GetProperty("operations").GetArrayLength());
		}

		[TestMethod]
		public async Task OperationsError_WhenTheServiceCannotBeDescribed_IsContainedAndLoggedServerSide()
		{
			var logs = new ListLoggerProvider();
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddSingleton<ILoggerProvider>(logs);
				},
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						endpoints.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
						endpoints.MapSoapCoreUi();
					});
				});
			UiTestHost.Register(host, "/Broken.svc", typeof(UiBrokenService));
			UiTestHost.Register(host, "/Ui.svc", typeof(UiTestService));

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var ids = UiTestHost.ServiceIds(document);
			CollectionAssert.AreEqual(new[] { "service-svc", "broken-svc", "ui-svc" }, ids);

			var broken = UiTestHost.GetService(document, 1);
			Assert.AreEqual("Could not read the operations of this service.", broken.GetProperty("operationsError").GetString());
			Assert.AreEqual(0, broken.GetProperty("operations").GetArrayLength());
			Assert.AreEqual("UiBrokenService", broken.GetProperty("name").GetString());
			Assert.AreEqual("SoapCore.Tests.Ui.UiBrokenService", broken.GetProperty("serviceType").GetString());
			Assert.AreEqual("/Broken.svc", broken.GetProperty("path").GetString());

			// The failure of one endpoint must not touch the others.
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetService(document, 0).GetProperty("operationsError").ValueKind);
			Assert.IsTrue(UiTestHost.GetService(document, 0).GetProperty("operations").GetArrayLength() > 0);
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetService(document, 2).GetProperty("operationsError").ValueKind);
			CollectionAssert.AreEqual(new[] { "CustomAction", "Echo", "Fire" }, UiTestHost.OperationNames(UiTestHost.GetService(document, 2)));

			// The details stay in the server log. The API never names the exception.
			AssertNoExceptionDetails(body);
			var warnings = logs.Entries.Where(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Could not read the operations", StringComparison.Ordinal)).ToArray();
			Assert.AreEqual(1, warnings.Length);
			StringAssert.Contains(warnings[0].Message, "SoapCore.Tests.Ui.UiBrokenService");

			// The outcome is cached per service type: a second request answers the same without describing the type again.
			using var again = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			Assert.AreEqual("Could not read the operations of this service.", UiTestHost.GetService(again, 1).GetProperty("operationsError").GetString());
			Assert.AreEqual(1, logs.Entries.Count(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Could not read the operations", StringComparison.Ordinal)));
		}

		[TestMethod]
		public async Task OperationsError_WhenTheEntryCannotBeProjected_FallsBackForThatEntryOnly()
		{
			var logs = new ListLoggerProvider();
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddSingleton<ILoggerProvider>(logs);
				},
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});
			var registry = host.Services.GetRequiredService<ISoapEndpointRegistry>();
			registry.Register(new SoapEndpointInfo { Path = "/Before.svc", ServiceType = typeof(UiTestService), MessageVersions = new[] { MessageVersion.Soap11 } });
			registry.Register(new SoapEndpointInfo { Path = "/Odd.svc", ServiceType = typeof(UiTestService), MessageVersions = new ThrowingMessageVersions() });
			registry.Register(new SoapEndpointInfo { Path = "/After.svc", ServiceType = typeof(UiTestService), MessageVersions = new[] { MessageVersion.Soap11 } });

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			CollectionAssert.AreEqual(new[] { "before-svc", "odd-svc", "after-svc" }, UiTestHost.ServiceIds(document));

			var odd = UiTestHost.GetService(document, 1);
			Assert.AreEqual("Could not describe this endpoint.", odd.GetProperty("operationsError").GetString());
			Assert.AreEqual("/Odd.svc", odd.GetProperty("name").GetString());
			Assert.AreEqual("/Odd.svc", odd.GetProperty("path").GetString());
			Assert.AreEqual(string.Empty, odd.GetProperty("serviceType").GetString());
			Assert.AreEqual(0, odd.GetProperty("operations").GetArrayLength());
			Assert.AreEqual(0, odd.GetProperty("soapVersions").GetArrayLength());

			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetService(document, 0).GetProperty("operationsError").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetService(document, 2).GetProperty("operationsError").ValueKind);

			AssertNoExceptionDetails(body);
			Assert.IsTrue(logs.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Could not describe the SOAP endpoint", StringComparison.Ordinal)));
		}

		[TestMethod]
		public async Task Options_TitleAndTryItOut_AreReflected()
		{
			using var host = UiTestHost.CreateWithUi(
				null,
				options =>
				{
					options.Title = "My Explorer";
					options.EnableTryItOut = false;
				});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual("My Explorer", document.RootElement.GetProperty("title").GetString());
			Assert.IsFalse(document.RootElement.GetProperty("tryItOutEnabled").GetBoolean());
		}

		[TestMethod]
		public async Task Services_HostileRegistryValues_AreJsonEscaped()
		{
			using var host = UiTestHost.CreateWithUi(null);
			UiTestHost.Register(host, "/a\"><script>x</script>.svc", typeof(UiTestService));

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);

			Assert.IsFalse(body.Contains('<'), "The JSON must not contain a raw '<'.");
			Assert.IsFalse(body.Contains('>'), "The JSON must not contain a raw '>'.");
			Assert.IsFalse(body.Contains('&'), "The JSON must not contain a raw '&'.");

			using var document = JsonDocument.Parse(body);
			var svc = UiTestHost.GetService(document, 0);
			StringAssert.Contains(svc.GetProperty("path").GetString(), "%22%3E%3Cscript%3E");
			Assert.AreEqual("a-script-x-script-svc", svc.GetProperty("id").GetString());
		}

		[TestMethod]
		public async Task Services_GenericServiceType_HasNoAssemblyInformation()
		{
			using var host = UiTestHost.CreateWithUi(null);
			UiTestHost.Register(host, "/Generic.svc", typeof(UiGenericService<UiTestService>));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var serviceType = UiTestHost.GetService(document, 0).GetProperty("serviceType").GetString();
			Assert.AreEqual("SoapCore.Tests.Ui.UiGenericService<SoapCore.Tests.Ui.UiTestService>", serviceType);
			Assert.IsFalse(serviceType.Contains("Version=", StringComparison.Ordinal));
			Assert.IsFalse(serviceType.Contains("PublicKeyToken", StringComparison.Ordinal));
			Assert.IsFalse(serviceType.Contains(','));
		}

		private static void AssertNoExceptionDetails(string body)
		{
			foreach (var detail in new[] { "NotImplementedException", "InvalidOperationException", "cannot be read", "StackTrace", "   at " })
			{
				Assert.IsFalse(body.Contains(detail, StringComparison.Ordinal), "The API must not leak exception details, found: " + detail);
			}
		}

		// A message version list whose enumeration fails: describing the entry that carries it throws.
		private sealed class ThrowingMessageVersions : IReadOnlyList<MessageVersion>
		{
			public int Count => 1;

			public MessageVersion this[int index] => throw new InvalidOperationException("The versions cannot be read.");

			public IEnumerator<MessageVersion> GetEnumerator()
			{
				throw new InvalidOperationException("The versions cannot be read.");
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}
	}
}
