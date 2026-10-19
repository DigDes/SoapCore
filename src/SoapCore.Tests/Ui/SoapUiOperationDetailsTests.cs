using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// GET {pattern}/api/services with SoapUiOptions.ExposeOperationDetails: the parameters, return type, faults and message contract flags of each operation.
	// The details are opt-in because they reveal CLR type names, so most of these tests also check that nothing leaks when the option is off.
	[TestClass]
	public class SoapUiOperationDetailsTests
	{
		private static readonly string[] AllOperations = { "Add", "Cheap", "Context", "Contracted", "Increment", "Items", "Load", "Notify", "Risky", "Split", "Touch", "TryParse" };

		private static readonly string[] DetailMembers = { "parameters", "returns", "faults", "messageContract" };

		[TestMethod]
		public async Task Details_AreOffByDefault_AndEveryDetailIsNull()
		{
			using var host = CreateHost(null);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			Assert.IsFalse(document.RootElement.GetProperty("operationDetailsEnabled").GetBoolean());
			CollectionAssert.AreEqual(AllOperations, UiTestHost.OperationNames(UiTestHost.GetService(document, 0)));
			AssertDetailsAreNull(document);

			// Nothing about the CLR types of the contract reaches the client.
			foreach (var leak in new[] { "UiDetailsModels", "UiShapeModels", "System." })
			{
				Assert.IsFalse(body.Contains(leak, StringComparison.Ordinal), "The JSON must not contain " + leak);
			}
		}

		[TestMethod]
		public async Task Details_WhenEnabled_AreReportedWithTheJsonShapeOfEveryOperation()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.IsTrue(document.RootElement.GetProperty("operationDetailsEnabled").GetBoolean());
			var svc = UiTestHost.GetService(document, 0);
			CollectionAssert.AreEqual(AllOperations, UiTestHost.OperationNames(svc));

			foreach (var operation in svc.GetProperty("operations").EnumerateArray())
			{
				CollectionAssert.AreEqual(
					new[] { "name", "soapAction", "isOneWay", "contract", "contractNamespace", "description", "parameters", "returns", "faults", "messageContract" },
					UiTestHost.PropertyNames(operation));
				Assert.AreEqual(JsonValueKind.Array, operation.GetProperty("parameters").ValueKind);
				Assert.AreEqual(JsonValueKind.Object, operation.GetProperty("returns").ValueKind);
				Assert.AreEqual(JsonValueKind.Array, operation.GetProperty("faults").ValueKind, "An operation without faults has an empty list, not null.");
				Assert.AreEqual(JsonValueKind.Object, operation.GetProperty("messageContract").ValueKind);
			}

			var add = UiTestHost.GetOperation(svc, "Add");
			CollectionAssert.AreEqual(new[] { "name", "type", "direction", "description" }, UiTestHost.PropertyNames(add.GetProperty("parameters")[0]));
			CollectionAssert.AreEqual(new[] { "type" }, UiTestHost.PropertyNames(add.GetProperty("returns")));
			CollectionAssert.AreEqual(new[] { "request", "response" }, UiTestHost.PropertyNames(add.GetProperty("messageContract")));
		}

		[TestMethod]
		public async Task Parameters_ListNameTypeAndDirectionInMethodOrder()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			CollectionAssert.AreEqual(new[] { "left:in:System.Int32", "right:in:System.Int32" }, Parameters(svc, "Add"));
			CollectionAssert.AreEqual(new[] { "text:in:System.String", "value:out:System.Int32" }, Parameters(svc, "TryParse"));
			CollectionAssert.AreEqual(new[] { "counter:ref:System.Int32" }, Parameters(svc, "Increment"));
			CollectionAssert.AreEqual(
				new[] { "text:in:System.String", "items:out:System.Collections.Generic.List<SoapCore.Tests.Ui.UiShapeModels+Item>", "separator:ref:System.String" },
				Parameters(svc, "Split"));
			CollectionAssert.AreEqual(new[] { "limit:in:System.Nullable<System.Int32>", "tags:in:System.String[]", "id:in:System.Guid" }, Parameters(svc, "Items"));
			CollectionAssert.AreEqual(new[] { "message:in:System.String" }, Parameters(svc, "Notify"));
			Assert.AreEqual(0, Parameters(svc, "Cheap").Length);
		}

		[TestMethod]
		public async Task Parameters_ThatAreNotPartOfTheSoapMessage_AreSkipped()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			CollectionAssert.AreEqual(new[] { "key:in:System.String" }, Parameters(svc, "Load"), "The CancellationToken is not listed.");
			CollectionAssert.AreEqual(new[] { "text:in:System.String" }, Parameters(svc, "Context"), "The HttpContext is not listed.");
		}

		[TestMethod]
		[DataRow("Add", "System.String")]
		[DataRow("TryParse", "System.Boolean")]
		[DataRow("Split", "System.Boolean")]
		[DataRow("Increment", "void")]
		[DataRow("Notify", "void")]
		[DataRow("Touch", "void")]
		[DataRow("Load", "SoapCore.Tests.Ui.UiShapeModels+Reply")]
		[DataRow("Cheap", "System.Int32")]
		[DataRow("Items", "System.Collections.Generic.List<SoapCore.Tests.Ui.UiShapeModels+Item>")]
		[DataRow("Contracted", "SoapCore.Tests.Ui.UiDetailsModels+Response")]
		public async Task Returns_AreFormattedAndTasksAreUnwrapped(string operation, string expected)
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var returns = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), operation).GetProperty("returns");
			Assert.AreEqual(expected, returns.GetProperty("type").GetString());
		}

		[TestMethod]
		public async Task Faults_AreTheDeclaredFaultTypesListedOnce()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			CollectionAssert.AreEqual(
				new[] { "SoapCore.Tests.Ui.UiDetailsModels+Fault", "SoapCore.Tests.Ui.UiDetailsModels+OtherFault" },
				Faults(svc, "Risky").OrderBy(name => name, StringComparer.Ordinal).ToArray(),
				"Risky declares Fault twice and OtherFault once.");
			Assert.AreEqual(0, Faults(svc, "Add").Length);
		}

		[TestMethod]
		public async Task MessageContract_FlagsTellWhetherTheRequestAndTheResponseAreMessageContracts()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var svc = UiTestHost.GetService(document, 0);
			var contracted = UiTestHost.GetOperation(svc, "Contracted").GetProperty("messageContract");
			Assert.IsTrue(contracted.GetProperty("request").GetBoolean());
			Assert.IsTrue(contracted.GetProperty("response").GetBoolean());
			CollectionAssert.AreEqual(new[] { "request:in:SoapCore.Tests.Ui.UiDetailsModels+Request" }, Parameters(svc, "Contracted"));

			var add = UiTestHost.GetOperation(svc, "Add").GetProperty("messageContract");
			Assert.IsFalse(add.GetProperty("request").GetBoolean());
			Assert.IsFalse(add.GetProperty("response").GetBoolean());
		}

		[TestMethod]
		public async Task Details_NeverContainAssemblyQualifiedNames()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);
			UiTestHost.Register(host, "/Limits.svc", typeof(IUiLimitsService));
			UiTestHost.Register(host, "/Shape.svc", typeof(UiShapeService));
			UiTestHost.Register(host, "/Generic.svc", typeof(UiGenericService<UiDetailsModels.Fault>));

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			foreach (var forbidden in new[] { ", Version=", "PublicKeyToken", "Culture=" })
			{
				Assert.IsFalse(body.Contains(forbidden, StringComparison.Ordinal), "The JSON must not contain " + forbidden);
			}

			var names = TypeNames(document);
			Assert.IsTrue(names.Length > 100, "The details of every operation should have been listed.");
			foreach (var name in names)
			{
				Assert.IsFalse(name.Contains('`'), "No arity marker expected in " + name);
				Assert.IsFalse(name.EndsWith('&'), "No by-ref marker expected in " + name);
				Assert.IsFalse(name.Contains("Version=", StringComparison.Ordinal), "No assembly information expected in " + name);
			}
		}

		[TestMethod]
		public async Task Details_AreCapped()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);
			UiTestHost.Register(host, "/Limits.svc", typeof(IUiLimitsService));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var limits = UiTestHost.GetService(document, 1);
			var parameters = Parameters(limits, "Many");
			Assert.AreEqual(100, parameters.Length, "At most 100 parameters are listed.");
			Assert.AreEqual("p001:in:System.Int32", parameters[0]);
			Assert.AreEqual("p100:in:System.Int32", parameters[99]);

			var faults = Faults(limits, "Faulty");
			Assert.AreEqual(20, faults.Length, "At most 20 faults are listed.");
			Assert.AreEqual(20, faults.Distinct(StringComparer.Ordinal).Count());
			foreach (var fault in faults)
			{
				StringAssert.StartsWith(fault, "SoapCore.Tests.Ui.UiDetailsModels+KeyedFault<");
			}
		}

		[TestMethod]
		public async Task Details_ToggledAtRuntime_NeverServeTheAnswerOfTheOtherSetting()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);
			var liveOptions = host.Services.GetRequiredService<IOptions<SoapUiOptions>>().Value;

			// The cache is filled with the option on, then the option goes off: the details must disappear at once.
			await AssertAnswerAsync(host, expectDetails: true);

			liveOptions.ExposeOperationDetails = false;
			await AssertAnswerAsync(host, expectDetails: false);

			liveOptions.ExposeOperationDetails = true;
			await AssertAnswerAsync(host, expectDetails: true);

			liveOptions.ExposeOperationDetails = false;
			await AssertAnswerAsync(host, expectDetails: false);
		}

		[TestMethod]
		public async Task Details_OfOneHost_DoNotReachAnotherHost()
		{
			using var withDetails = CreateHost(options => options.ExposeOperationDetails = true);
			using var withoutDetails = CreateHost(null);

			await AssertAnswerAsync(withDetails, expectDetails: true);
			await AssertAnswerAsync(withoutDetails, expectDetails: false);
			await AssertAnswerAsync(withDetails, expectDetails: true);
		}

		[TestMethod]
		public async Task Details_WhenAnotherServiceCannotBeDescribed_AreStillListed()
		{
			using var host = CreateHost(options => options.ExposeOperationDetails = true);
			UiTestHost.Register(host, "/Broken.svc", typeof(UiBrokenService));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var broken = UiTestHost.GetService(document, 1);
			Assert.AreEqual("Could not read the operations of this service.", broken.GetProperty("operationsError").GetString());
			Assert.AreEqual(0, broken.GetProperty("operations").GetArrayLength());
			CollectionAssert.AreEqual(new[] { "left:in:System.Int32", "right:in:System.Int32" }, Parameters(UiTestHost.GetService(document, 0), "Add"));
		}

		// The explorer with /Details.svc registered (IUiDetailsService as the service type, so no implementation is needed).
		private static TestServer CreateHost(Action<SoapUiOptions> configureUi)
		{
			var host = UiTestHost.CreateWithUi(null, configureUi);
			UiTestHost.Register(host, "/Details.svc", typeof(IUiDetailsService));

			return host;
		}

		private static async Task AssertAnswerAsync(TestServer host, bool expectDetails)
		{
			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			Assert.AreEqual(expectDetails, document.RootElement.GetProperty("operationDetailsEnabled").GetBoolean());
			if (expectDetails)
			{
				var svc = UiTestHost.GetService(document, 0);
				CollectionAssert.AreEqual(new[] { "left:in:System.Int32", "right:in:System.Int32" }, Parameters(svc, "Add"));
				Assert.AreEqual("System.String", UiTestHost.GetOperation(svc, "Add").GetProperty("returns").GetProperty("type").GetString());
			}
			else
			{
				AssertDetailsAreNull(document);
				Assert.IsFalse(body.Contains("System.", StringComparison.Ordinal), "No CLR type name may be served while the option is off.");
			}
		}

		private static void AssertDetailsAreNull(JsonDocument document)
		{
			foreach (var service in document.RootElement.GetProperty("services").EnumerateArray())
			{
				foreach (var operation in service.GetProperty("operations").EnumerateArray())
				{
					foreach (var member in DetailMembers)
					{
						Assert.AreEqual(JsonValueKind.Null, operation.GetProperty(member).ValueKind, member + " must be null while the option is off.");
					}
				}
			}
		}

		private static string[] Parameters(JsonElement service, string operationName)
		{
			return UiTestHost.GetOperation(service, operationName).GetProperty("parameters").EnumerateArray()
				.Select(parameter => parameter.GetProperty("name").GetString() + ":" + parameter.GetProperty("direction").GetString() + ":" + parameter.GetProperty("type").GetString())
				.ToArray();
		}

		private static string[] Faults(JsonElement service, string operationName)
		{
			return UiTestHost.Strings(UiTestHost.GetOperation(service, operationName).GetProperty("faults"));
		}

		// Every type name of every parameter, return value and fault in the document.
		private static string[] TypeNames(JsonDocument document)
		{
			var names = new List<string>();
			foreach (var service in document.RootElement.GetProperty("services").EnumerateArray())
			{
				foreach (var operation in service.GetProperty("operations").EnumerateArray())
				{
					names.AddRange(operation.GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("type").GetString()));
					names.Add(operation.GetProperty("returns").GetProperty("type").GetString());
					names.AddRange(UiTestHost.Strings(operation.GetProperty("faults")));
				}
			}

			return names.ToArray();
		}
	}
}
