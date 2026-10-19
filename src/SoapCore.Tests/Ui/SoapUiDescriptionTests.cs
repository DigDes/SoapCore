using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// GET {pattern}/api/services: the descriptions of services, operations and parameters that come from [Description] attributes.
	// The descriptions from XML documentation files are in SoapUiXmlCommentsTests.
	[TestClass]
	public class SoapUiDescriptionTests
	{
		[TestMethod]
		public async Task Attributes_DescribeTheServiceItsOperationsAndTheirParameters()
		{
			using var host = CreateHost(typeof(IUiDescribedService), options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("A service described with attributes.", service.GetProperty("description").GetString());

			var add = UiTestHost.GetOperation(service, "Add");
			Assert.AreEqual("Adds two numbers.", add.GetProperty("description").GetString());
			CollectionAssert.AreEqual(
				new[] { "left:The left operand.", "right:The right operand." },
				Parameters(add));

			var tryParse = UiTestHost.GetOperation(service, "TryParse");
			Assert.AreEqual("The parsed number.", tryParse.GetProperty("description").GetString());
			CollectionAssert.AreEqual(
				new[] { "text:", "value:The number, when the text is one." },
				Parameters(tryParse),
				"An out parameter is described too, and a parameter without a description has a null one.");
		}

		[TestMethod]
		public async Task Attributes_ASmallPieceOfTheContractWithoutDescription_HasNullDescriptions()
		{
			using var host = CreateHost(typeof(IUiDescribedService), options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var plain = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Plain");
			Assert.AreEqual(JsonValueKind.Null, plain.GetProperty("description").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, plain.GetProperty("parameters")[0].GetProperty("description").ValueKind);
		}

		[TestMethod]
		public async Task Descriptions_OfOperations_AreServedWhetherOrNotTheDetailsAre()
		{
			using var host = CreateHost(typeof(IUiDescribedService), null);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("A service described with attributes.", service.GetProperty("description").GetString());
			var add = UiTestHost.GetOperation(service, "Add");
			Assert.AreEqual("Adds two numbers.", add.GetProperty("description").GetString());

			// The parameters, and with them their descriptions, are details: they stay out unless ExposeOperationDetails is on.
			Assert.AreEqual(JsonValueKind.Null, add.GetProperty("parameters").ValueKind);
			Assert.IsFalse(body.Contains("The left operand.", StringComparison.Ordinal), "A parameter description must not be served while the details are off.");
			Assert.IsFalse(body.Contains("The right operand.", StringComparison.Ordinal));
		}

		[TestMethod]
		public async Task Descriptions_FollowTheDetailsOptionOnEveryRequest_ForParameters()
		{
			using var host = CreateHost(typeof(IUiDescribedService), options => options.ExposeOperationDetails = true);
			var liveOptions = host.Services.GetRequiredService<IOptions<SoapUiOptions>>().Value;

			using (var withDetails = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath))
			{
				CollectionAssert.AreEqual(
					new[] { "left:The left operand.", "right:The right operand." },
					Parameters(UiTestHost.GetOperation(UiTestHost.GetService(withDetails, 0), "Add")));
			}

			liveOptions.ExposeOperationDetails = false;
			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);

			Assert.IsFalse(body.Contains("The left operand.", StringComparison.Ordinal), "Switching the details off must hide the parameter descriptions at once.");
			using var withoutDetails = JsonDocument.Parse(body);
			Assert.AreEqual("Adds two numbers.", UiTestHost.GetOperation(UiTestHost.GetService(withoutDetails, 0), "Add").GetProperty("description").GetString());
		}

		[TestMethod]
		public async Task Attributes_AreCleanedUpIntoOneLineOfPlainText()
		{
			using var host = CreateHost(typeof(IUiDescribedService), null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("Spaces and line breaks cleaned.", UiTestHost.GetOperation(service, "Messy").GetProperty("description").GetString());
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetOperation(service, "Blank").GetProperty("description").ValueKind, "An empty description is no description.");
		}

		[TestMethod]
		public async Task Attributes_LongerThan500Characters_AreCutAndEndWithDots()
		{
			using var host = CreateHost(typeof(IUiDescribedService), null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var text = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Long").GetProperty("description").GetString();
			Assert.AreEqual(500, text.Length);
			Assert.IsTrue(text.EndsWith("...", StringComparison.Ordinal));
			Assert.IsTrue(UiDescribedConstants.Long.StartsWith(text.Substring(0, 497), StringComparison.Ordinal), "The start of the text is kept.");
		}

		[TestMethod]
		public async Task Attributes_ArePlainText_MarkupIsNeitherStrippedNorInterpreted()
		{
			using var host = CreateHost(typeof(IUiDescribedService), null);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var text = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Markup").GetProperty("description").GetString();
			Assert.AreEqual("Some <b>bold</b> & <script>alert(1)</script> text", text);
			Assert.IsFalse(body.Contains('<'), "The JSON must not contain a raw '<'.");
			Assert.IsFalse(body.Contains('>'), "The JSON must not contain a raw '>'.");
			Assert.IsFalse(body.Contains('&'), "The JSON must not contain a raw '&'.");
		}

		[TestMethod]
		public async Task UseDescriptionAttributes_False_IgnoresTheAttributes()
		{
			using var host = CreateHost(
				typeof(IUiDescribedService),
				options =>
				{
					options.UseDescriptionAttributes = false;
					options.ExposeOperationDetails = true;
				});

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("description").ValueKind);
			foreach (var operation in service.GetProperty("operations").EnumerateArray())
			{
				Assert.AreEqual(JsonValueKind.Null, operation.GetProperty("description").ValueKind, operation.GetProperty("name").GetString());
				foreach (var parameter in operation.GetProperty("parameters").EnumerateArray())
				{
					Assert.AreEqual(JsonValueKind.Null, parameter.GetProperty("description").ValueKind);
				}
			}

			Assert.IsFalse(body.Contains("Adds two numbers.", StringComparison.Ordinal));
			Assert.IsFalse(body.Contains("The left operand.", StringComparison.Ordinal));
		}

		[TestMethod]
		public async Task Attributes_OfTheImplementation_AreUsedWhenTheContractSaysNothing()
		{
			using var host = CreateHost(typeof(UiImplementationDescribedService), options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("From the implementation type.", service.GetProperty("description").GetString());

			var work = UiTestHost.GetOperation(service, "Work");
			Assert.AreEqual("From the implementation.", work.GetProperty("description").GetString());
			CollectionAssert.AreEqual(new[] { "input:Implementation parameter." }, Parameters(work));
		}

		[TestMethod]
		public async Task Attributes_OfTheContract_WinOverTheImplementation()
		{
			using var host = CreateHost(typeof(UiImplementationDescribedService), options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var both = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Both");
			Assert.AreEqual("From the contract.", both.GetProperty("description").GetString());
			CollectionAssert.AreEqual(new[] { "input:Contract parameter." }, Parameters(both));
		}

		[TestMethod]
		public async Task Attributes_OfTheInterfaceAsServiceType_NeedNoImplementation()
		{
			// Same as above, with the contract registered as the service type (what UseSoapEndpoint<IContract> does).
			using var host = CreateHost(typeof(IUiImplementationDescribedService), options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("description").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetOperation(service, "Work").GetProperty("description").ValueKind);
			Assert.AreEqual("From the contract.", UiTestHost.GetOperation(service, "Both").GetProperty("description").GetString());
		}

		[TestMethod]
		public async Task Attributes_ThatCannotBeRead_LeaveTheDescriptionNullAndAreLoggedOnce()
		{
			var logs = new ListLoggerProvider();
			using var host = CreateHost(typeof(IUiThrowingDescriptionService), options => options.ExposeOperationDetails = true, logs);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("operationsError").ValueKind, "The service is still described.");
			CollectionAssert.AreEqual(new[] { "Broken", "Fine" }, UiTestHost.OperationNames(service));

			var broken = UiTestHost.GetOperation(service, "Broken");
			Assert.AreEqual(JsonValueKind.Null, broken.GetProperty("description").ValueKind);
			CollectionAssert.AreEqual(new[] { "input:", "other:" }, Parameters(broken), "The details of the operation survive a description that cannot be read.");

			// What can be read is not touched by the failures around it.
			var fine = UiTestHost.GetOperation(service, "Fine");
			Assert.AreEqual("Still fine.", fine.GetProperty("description").GetString());
			CollectionAssert.AreEqual(new[] { "input:Fine parameter." }, Parameters(fine));

			// The exception stays in the server log, once per description (the operation and its first parameter), and never reaches the API.
			foreach (var detail in new[] { "The description cannot be read.", "InvalidOperationException", "   at " })
			{
				Assert.IsFalse(body.Contains(detail, StringComparison.Ordinal), "The API must not leak: " + detail);
			}

			Assert.AreEqual(2, Warnings(logs).Length);
			Assert.IsTrue(Warnings(logs).Any(message => message.Contains("operation Broken", StringComparison.Ordinal)));
			Assert.IsTrue(Warnings(logs).Any(message => message.Contains("parameter input of operation Broken", StringComparison.Ordinal)));

			// The outcome is cached with the operations: another request neither reads nor logs again.
			using var again = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			Assert.AreEqual(2, Warnings(logs).Length);
		}

		private static TestServer CreateHost(Type serviceType, Action<SoapUiOptions> configureUi, ListLoggerProvider logs = null)
		{
			var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi(configureUi);
					if (logs != null)
					{
						services.AddSingleton<ILoggerProvider>(logs);
					}
				},
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});
			UiTestHost.Register(host, "/Described.svc", serviceType);

			return host;
		}

		// "name:description" for each parameter, the description being empty when it is null.
		private static string[] Parameters(JsonElement operation)
		{
			return operation.GetProperty("parameters").EnumerateArray()
				.Select(parameter => parameter.GetProperty("name").GetString() + ":" + (parameter.GetProperty("description").GetString() ?? string.Empty))
				.ToArray();
		}

		private static string[] Warnings(ListLoggerProvider logs)
		{
			return logs.Entries
				.Where(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Could not read the description", StringComparison.Ordinal))
				.Select(entry => entry.Message)
				.ToArray();
		}
	}
}
