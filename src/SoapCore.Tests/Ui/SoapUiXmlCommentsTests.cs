using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// GET {pattern}/api/services with SoapUiOptions.IncludeXmlComments: the descriptions that come from XML documentation files.
	// Each test writes the XML file it needs, so the member ids below are the ones the C# compiler writes for the fixture contracts.
	[TestClass]
	public class SoapUiXmlCommentsTests
	{
		private const string Contract = "SoapCore.Tests.Ui.IUiXmlDocService";

		private const string WarningText = "Could not read the XML documentation file";

		[TestMethod]
		public async Task Summaries_OfTheServiceAndItsOperations_AreServed()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("The String service.", service.GetProperty("description").GetString());
			Assert.AreEqual("Returns the text repeated count times.", DescriptionOf(UiTestHost.GetOperation(service, "Summary")));
			Assert.AreEqual("Nothing to pass.", DescriptionOf(UiTestHost.GetOperation(service, "NoParameters")));
		}

		[TestMethod]
		public async Task Parameters_AreDescribedByTheParamElements_WhenTheDetailsAreOn()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var summary = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Summary");
			CollectionAssert.AreEqual(new[] { "text:The text to repeat.", "count:How many times." }, Parameters(summary));
		}

		[TestMethod]
		public async Task Parameters_AndTheirDescriptions_StayOutWhenTheDetailsAreOff()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath, options => options.ExposeOperationDetails = false);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var summary = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Summary");
			Assert.AreEqual("Returns the text repeated count times.", DescriptionOf(summary), "The description of the operation is not a detail.");
			Assert.AreEqual(JsonValueKind.Null, summary.GetProperty("parameters").ValueKind);
			Assert.IsFalse(body.Contains("The text to repeat.", StringComparison.Ordinal), "A parameter description must not be served while the details are off.");
			Assert.IsFalse(body.Contains("How many times.", StringComparison.Ordinal));
		}

		[TestMethod]
		public async Task Markup_IsConvertedIntoPlainText()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			// cref gives its last segment, paramref and langword their value, c and para their text, and a link its content.
			var markup = UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Markup");
			Assert.AreEqual("Uses List and Summary with value, code and null. See the docs. Second paragraph.", DescriptionOf(markup));
			CollectionAssert.AreEqual(new[] { "value:The T value." }, Parameters(markup));
		}

		[TestMethod]
		public async Task MemberIds_CoverGenericsArraysByRefNullableAndNestedTypes()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			AssertOperation(service, "NoParameters", "Nothing to pass.");
			AssertOperation(service, "Generic", "Generic parameters.", "values:The values.", "map:The map.");
			AssertOperation(service, "Arrays", "Arrays of arrays.", "values:", "jagged:", "grid:The grid.");
			AssertOperation(service, "TryRead", "Out and ref parameters.", "text:", "value:The parsed value.", "mode:The mode.");
			AssertOperation(service, "Nested", "Nested types.", "inner:", "generic:The generic nested value.");
			AssertOperation(service, "Optional", "An optional value.", "value:");
		}

		[TestMethod]
		public async Task Whitespace_InTheXml_IsCollapsed()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual("Lots of whitespace and line breaks.", DescriptionOf(UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Whitespace")));
		}

		[TestMethod]
		public async Task Entities_AndCData_BecomePlainText_AndTheJsonNeverCarriesMarkup()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			Assert.AreEqual(
				"Use <b>bold</b> & <script>alert(1)</script> text.",
				DescriptionOf(UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Escapes")));
			Assert.IsFalse(body.Contains('<'), "The JSON must not contain a raw '<'.");
			Assert.IsFalse(body.Contains('>'), "The JSON must not contain a raw '>'.");
			Assert.IsFalse(body.Contains('&'), "The JSON must not contain a raw '&'.");
		}

		[TestMethod]
		public async Task MembersWithoutASummary_HaveNoDescription()
		{
			using var file = UiTempXmlFile.CreateDoc(
				UiTempXmlFile.Member("M:" + Contract + ".Inherited(System.String)", "<inheritdoc/>"),
				UiTempXmlFile.Member("M:" + Contract + ".NoParameters", "<summary>   </summary>"),
				UiTempXmlFile.Member("M:" + Contract + ".Whitespace(System.String)", "<summary/>"));
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			foreach (var name in new[] { "Inherited", "NoParameters", "Whitespace" })
			{
				Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetOperation(service, name).GetProperty("description").ValueKind, name);
			}
		}

		[TestMethod]
		[DataRow(1, 1, false)]
		[DataRow(499, 499, false)]
		[DataRow(500, 500, false)]
		[DataRow(501, 500, true)]
		[DataRow(20000, 500, true)]
		public async Task Summaries_AreCappedAt500Characters(int length, int expectedLength, bool cut)
		{
			var text = new string('x', length);
			using var file = UiTempXmlFile.CreateDoc(UiTempXmlFile.Member("M:" + Contract + ".Boundary(System.String)", "<summary>" + text + "</summary>"));
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var description = DescriptionOf(UiTestHost.GetOperation(UiTestHost.GetService(document, 0), "Boundary"));
			Assert.AreEqual(expectedLength, description.Length);
			if (cut)
			{
				Assert.IsTrue(description.EndsWith("...", StringComparison.Ordinal), "A text that was cut ends with dots.");
				Assert.AreEqual(text.Substring(0, 497), description.Substring(0, 497), "The start of the text is kept.");
			}
			else
			{
				Assert.AreEqual(text, description);
			}
		}

		[TestMethod]
		public async Task NestedContractTypes_AreWrittenWithDots()
		{
			using var file = UiTempXmlFile.CreateDoc(
				UiTempXmlFile.Member("T:SoapCore.Tests.Ui.UiXmlDocContainer.INestedService", "<summary>A nested contract.</summary>"),
				UiTempXmlFile.Member("M:SoapCore.Tests.Ui.UiXmlDocContainer.INestedService.Ping(System.String)", "<summary>Pings.</summary><param name=\"text\">The text.</param>"));
			using var host = CreateHost(typeof(UiXmlDocContainer.INestedService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("A nested contract.", service.GetProperty("description").GetString());
			AssertOperation(service, "Ping", "Pings.", "text:The text.");
		}

		[TestMethod]
		public async Task GenericContracts_AreWrittenWithTheTypeParameterMarkers()
		{
			var members = new[]
			{
				UiTempXmlFile.Member("T:SoapCore.Tests.Ui.IUiXmlDocGenericService`1", "<summary>A generic contract.</summary>"),
				UiTempXmlFile.Member(
					"M:SoapCore.Tests.Ui.IUiXmlDocGenericService`1.Echo(`0,System.Collections.Generic.List{`0})",
					"<summary>Echoes the value.</summary>",
					"<param name=\"value\">The value.</param>",
					"<param name=\"values\">More values.</param>"),
			};

			using var file = UiTempXmlFile.CreateDoc(members);
			using var host = CreateHost(typeof(UiXmlDocGenericService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("A generic contract.", service.GetProperty("description").GetString());
			AssertOperation(service, "Echo", "Echoes the value.", "value:The value.", "values:More values.");
		}

		[TestMethod]
		public async Task GenericServiceTypes_AndTheirImplementations_AreWrittenWithTheArity()
		{
			using var file = UiTempXmlFile.CreateDoc(
				UiTempXmlFile.Member("T:SoapCore.Tests.Ui.UiGenericService`1", "<summary>A generic service type.</summary>"),
				UiTempXmlFile.Member("M:SoapCore.Tests.Ui.IUiTestService.Echo(System.String)", "<summary>Echoes the text.</summary>"),
				UiTempXmlFile.Member("M:SoapCore.Tests.Ui.UiGenericService`1.Custom", "<summary>Custom, from the generic implementation.</summary>"));
			using var host = CreateHost(typeof(UiGenericService<UiTestService>), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("A generic service type.", service.GetProperty("description").GetString());
			Assert.AreEqual("Echoes the text.", DescriptionOf(UiTestHost.GetOperation(service, "Echo")));
			Assert.AreEqual("Custom, from the generic implementation.", DescriptionOf(UiTestHost.GetOperation(service, "CustomAction")));
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetOperation(service, "Fire").GetProperty("description").ValueKind);
		}

		[TestMethod]
		public async Task Attributes_WinOverTheXml_AndTheXmlFillsTheGaps()
		{
			using var file = DescribedFile();
			using var host = CreateHost(typeof(IUiDescribedService), file.FilePath);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("A service described with attributes.", service.GetProperty("description").GetString());
			AssertOperation(service, "Add", "Adds two numbers.", "left:The left operand.", "right:The right operand.");

			// No attribute on Plain: the XML describes it.
			AssertOperation(service, "Plain", "XML plain.", "text:XML text.");
		}

		[TestMethod]
		public async Task UseDescriptionAttributes_False_LeavesTheXmlAlone()
		{
			using var file = DescribedFile();
			using var host = CreateHost(typeof(IUiDescribedService), file.FilePath, options => options.UseDescriptionAttributes = false);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("XML service.", service.GetProperty("description").GetString());
			AssertOperation(service, "Add", "XML adds.", "left:XML left.", "right:XML right.");
			AssertOperation(service, "Plain", "XML plain.", "text:XML text.");

			// What only an attribute says is gone.
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetOperation(service, "Messy").GetProperty("description").ValueKind);
			Assert.IsFalse(body.Contains("Adds two numbers.", StringComparison.Ordinal));
		}

		[TestMethod]
		public async Task Xml_OfTheImplementation_IsUsedAfterTheXmlOfTheContract()
		{
			const string implementation = "SoapCore.Tests.Ui.UiImplementationDescribedService";
			const string contract = "SoapCore.Tests.Ui.IUiImplementationDescribedService";
			using var file = UiTempXmlFile.CreateDoc(
				UiTempXmlFile.Member("T:" + implementation, "<summary>XML implementation type.</summary>"),
				UiTempXmlFile.Member("M:" + implementation + ".Work(System.String)", "<summary>XML from the implementation.</summary><param name=\"input\">XML implementation parameter.</param>"),
				UiTempXmlFile.Member("M:" + contract + ".Both(System.String)", "<summary>XML from the contract.</summary><param name=\"input\">XML contract parameter.</param>"),
				UiTempXmlFile.Member("M:" + implementation + ".Both(System.String)", "<summary>XML from the implementation, so it loses.</summary><param name=\"input\">XML implementation parameter, so it loses.</param>"));
			using var host = CreateHost(typeof(UiImplementationDescribedService), file.FilePath, options => options.UseDescriptionAttributes = false);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("XML implementation type.", service.GetProperty("description").GetString());
			AssertOperation(service, "Work", "XML from the implementation.", "input:XML implementation parameter.");
			AssertOperation(service, "Both", "XML from the contract.", "input:XML contract parameter.");
		}

		[TestMethod]
		public async Task SeveralFiles_AreConsultedInOrder()
		{
			using var first = UiTempXmlFile.CreateDoc(UiTempXmlFile.Member("M:" + Contract + ".Summary(System.String,System.Int32)", "<summary>From the first file.</summary>"));
			using var second = UiTempXmlFile.CreateDoc(
				UiTempXmlFile.Member("M:" + Contract + ".Summary(System.String,System.Int32)", "<summary>From the second file.</summary>"),
				UiTempXmlFile.Member("M:" + Contract + ".NoParameters", "<summary>Only in the second file.</summary>"));
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { first.FilePath, second.FilePath }, null, null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual("From the first file.", DescriptionOf(UiTestHost.GetOperation(service, "Summary")));
			Assert.AreEqual("Only in the second file.", DescriptionOf(UiTestHost.GetOperation(service, "NoParameters")));
		}

		[TestMethod]
		public async Task TheFile_IsReadOnce_AndNeverLocked()
		{
			var logs = new ListLoggerProvider();
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { file.FilePath }, null, logs);

			using (var first = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath))
			{
				Assert.AreEqual("The String service.", UiTestHost.GetService(first, 0).GetProperty("description").GetString());
			}

			// Gone from the disk, yet a service described later still gets its descriptions: nothing reads the file again.
			File.Delete(file.FilePath);
			Assert.IsFalse(File.Exists(file.FilePath), "The explorer must not keep the file open.");
			UiTestHost.Register(host, "/Nested.svc", typeof(UiXmlDocContainer.INestedService));

			using var second = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual("The String service.", UiTestHost.GetService(second, 0).GetProperty("description").GetString());
			Assert.AreEqual(2, second.RootElement.GetProperty("services").GetArrayLength());
			Assert.AreEqual(0, Warnings(logs).Length, "A file that was read is not read again, so it cannot fail later.");
		}

		[TestMethod]
		public async Task TheFile_IsOnlyReadWhenAServiceIsDescribed()
		{
			// Nothing to describe, nothing to read: the missing file is not even noticed.
			var logs = new ListLoggerProvider();
			using var host = CreateHost(null, new[] { MissingFilePath() }, null, logs);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(0, Warnings(logs).Length);
		}

		[TestMethod]
		public async Task AMissingFile_LogsOneWarning_AndTheExplorerGoesOn()
		{
			var logs = new ListLoggerProvider();
			var missing = MissingFilePath();
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { missing }, null, logs);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("operationsError").ValueKind);
			Assert.AreEqual(12, service.GetProperty("operations").GetArrayLength(), "Every operation is still listed.");
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("description").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetOperation(service, "Summary").GetProperty("description").ValueKind);

			var warnings = Warnings(logs);
			Assert.AreEqual(1, warnings.Length, "One warning for the file, not one per lookup.");
			StringAssert.Contains(warnings[0], Path.GetFileName(missing), "The log, which stays on the server, says which file.");

			// Asking again neither reads the file again nor logs again.
			using var again = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			Assert.AreEqual(1, Warnings(logs).Length);
			Assert.IsFalse(body.Contains(Path.GetFileName(missing), StringComparison.Ordinal), "The API must not name the file.");
		}

		[TestMethod]
		public async Task APathThatIsADirectory_IsHandledLikeAMissingFile()
		{
			var logs = new ListLoggerProvider();
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { Path.GetTempPath() }, null, logs);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(12, UiTestHost.GetService(document, 0).GetProperty("operations").GetArrayLength());
			Assert.AreEqual(1, Warnings(logs).Length);
		}

		[TestMethod]
		[DataRow("")]
		[DataRow("   ")]
		[DataRow("this is not xml at all")]
		[DataRow("<doc><members><member name=")]
		[DataRow("<doc><members><member name=\"T:SoapCore.Tests.Ui.IUiXmlDocService\"><summary>Never closed.</member></members></doc>")]
		[DataRow("<doc><members><member name=\"T:SoapCore.Tests.Ui.IUiXmlDocService\"><summary>Unknown entity &nothere;.</summary></member></members></doc>")]
		[DataRow("<doc><members><member name=\"T:SoapCore.Tests.Ui.IUiXmlDocService\"><summary>Fine.</summary></member></members></doc><doc/>")]
		public async Task AFileThatIsNotValidXml_LogsOneWarning_AndTheExplorerGoesOn(string content)
		{
			var logs = new ListLoggerProvider();
			using var file = UiTempXmlFile.Create(content);
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { file.FilePath }, null, logs);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("operationsError").ValueKind);
			Assert.AreEqual(12, service.GetProperty("operations").GetArrayLength());
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("description").ValueKind, "A file that is not valid is used for nothing, not even for the part that was fine.");
			Assert.AreEqual(1, Warnings(logs).Length);
			Assert.IsFalse(body.Contains(Path.GetFileName(file.FilePath), StringComparison.Ordinal), "The API must not name the file.");
		}

		[TestMethod]
		public async Task ADocumentTypeDeclaration_IsRefused_AndEntitiesAreNeverResolved()
		{
			// An external entity would put the content of a local file into the description. A DTD is refused before anything is resolved.
			using var secret = UiTempXmlFile.Create("SECRET-TOKEN-12345");
			var logs = new ListLoggerProvider();
			var xml = "<?xml version=\"1.0\"?>\n"
				+ "<!DOCTYPE doc [<!ENTITY xxe SYSTEM \"" + new Uri(secret.FilePath).AbsoluteUri + "\">]>\n"
				+ "<doc><members><member name=\"T:" + Contract + "\"><summary>Leak: &xxe;</summary></member></members></doc>";
			using var file = UiTempXmlFile.Create(xml);
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { file.FilePath }, null, logs);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			Assert.IsFalse(body.Contains("SECRET-TOKEN", StringComparison.Ordinal), "The content of the file must never reach the API.");
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetService(document, 0).GetProperty("description").ValueKind);
			Assert.AreEqual(1, Warnings(logs).Length);
		}

		[TestMethod]
		public async Task AnInternalEntityExpansion_IsRefused_NotExpanded()
		{
			var logs = new ListLoggerProvider();
			var xml = "<?xml version=\"1.0\"?>\n"
				+ "<!DOCTYPE doc [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">]>\n"
				+ "<doc><members><member name=\"T:" + Contract + "\"><summary>&b;</summary></member></members></doc>";
			using var file = UiTempXmlFile.Create(xml);
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { file.FilePath }, null, logs);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			Assert.IsFalse(body.Contains("aaaaaaaaaa", StringComparison.Ordinal), "The entity must not be expanded.");
			Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetService(document, 0).GetProperty("description").ValueKind);
			Assert.AreEqual(1, Warnings(logs).Length);
		}

		[TestMethod]
		public async Task OneBrokenFile_DoesNotTakeTheOthersDown()
		{
			var logs = new ListLoggerProvider();
			using var broken = UiTempXmlFile.Create("<doc><members><member");
			using var good = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), new[] { MissingFilePath(), broken.FilePath, good.FilePath }, null, logs);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual("The String service.", UiTestHost.GetService(document, 0).GetProperty("description").GetString());
			Assert.AreEqual(2, Warnings(logs).Length, "One warning for each file that cannot be read.");
		}

		[TestMethod]
		public async Task Paths_NeverReachTheApi()
		{
			using var file = StandardFile();
			using var host = CreateHost(typeof(IUiXmlDocService), file.FilePath);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);

			Assert.IsFalse(body.Contains(Path.GetFileName(file.FilePath), StringComparison.Ordinal));
			Assert.IsFalse(body.Contains(UiTempXmlFile.FilePrefix, StringComparison.Ordinal));
			Assert.IsFalse(body.Contains(".xml", StringComparison.OrdinalIgnoreCase));
			Assert.IsFalse(body.Contains("XmlCommentsFiles", StringComparison.OrdinalIgnoreCase));
		}

		private static TestServer CreateHost(Type serviceType, string xmlFile, Action<SoapUiOptions> configure = null)
		{
			return CreateHost(serviceType, new[] { xmlFile }, configure, null);
		}

		private static TestServer CreateHost(Type serviceType, string[] xmlFiles, Action<SoapUiOptions> configure, ListLoggerProvider logs)
		{
			var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi(options =>
					{
						foreach (var xmlFile in xmlFiles)
						{
							options.IncludeXmlComments(xmlFile);
						}

						options.ExposeOperationDetails = true;
						configure?.Invoke(options);
					});

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

			if (serviceType != null)
			{
				UiTestHost.Register(host, "/Xml.svc", serviceType);
			}

			return host;
		}

		private static string MissingFilePath()
		{
			return UiTempXmlFile.NewPath("missing-");
		}

		// Documentation for IUiXmlDocService in the format the compiler writes.
		private static UiTempXmlFile StandardFile()
		{
			var members = new[]
			{
				UiTempXmlFile.Member("T:" + Contract, "<summary>The <see cref=\"T:System.String\"/> service.</summary>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".Summary(System.String,System.Int32)",
					"<summary>Returns the <paramref name=\"text\"/> repeated <paramref name=\"count\"/> times.</summary>",
					"<param name=\"text\">The text to repeat.</param>",
					"<param name=\"count\">How many times.</param>"),
				UiTempXmlFile.Member("M:" + Contract + ".NoParameters", "<summary>Nothing to pass.</summary>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".Markup(System.String)",
					"<summary>Uses <see cref=\"T:System.Collections.Generic.List`1\"/> and ",
					"<see cref=\"M:" + Contract + ".Summary(System.String,System.Int32)\"/> with <paramref name=\"value\"/>, ",
					"<c>code</c> and <see langword=\"null\"/>. See <see href=\"https://example.com/docs\">the docs</see>.",
					"<para>Second paragraph.</para></summary>",
					"<param name=\"value\">The <typeparamref name=\"T\"/> value.</param>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".Generic(System.Collections.Generic.List{System.Int32},System.Collections.Generic.Dictionary{System.String,System.Collections.Generic.List{System.Int32}})",
					"<summary>Generic parameters.</summary>",
					"<param name=\"values\">The values.</param>",
					"<param name=\"map\">The map.</param>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".Arrays(System.Int32[],System.String[][],System.Int32[0:,0:])",
					"<summary>Arrays of arrays.</summary>",
					"<param name=\"grid\">The grid.</param>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".TryRead(System.String,System.Int32@,System.String@)",
					"<summary>Out and ref parameters.</summary>",
					"<param name=\"value\">The parsed value.</param>",
					"<param name=\"mode\">The mode.</param>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".Nested(SoapCore.Tests.Ui.UiXmlDocModels.Inner,SoapCore.Tests.Ui.UiXmlDocModels.Outer{System.Int32}.Inner{System.String})",
					"<summary>Nested types.</summary>",
					"<param name=\"generic\">The generic nested value.</param>"),
				UiTempXmlFile.Member("M:" + Contract + ".Optional(System.Nullable{System.Int32})", "<summary>An optional value.</summary>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".Whitespace(System.String)",
					"<summary>\n        Lots   of\n        whitespace\tand\n        line breaks.\n      </summary>"),
				UiTempXmlFile.Member(
					"M:" + Contract + ".Escapes(System.String)",
					"<summary>Use &lt;b&gt;bold&lt;/b&gt; &amp; <![CDATA[<script>alert(1)</script>]]> text.</summary>"),
			};

			return UiTempXmlFile.CreateDoc(members);
		}

		// Documentation for IUiDescribedService, which also carries [Description] attributes.
		private static UiTempXmlFile DescribedFile()
		{
			const string described = "SoapCore.Tests.Ui.IUiDescribedService";
			var members = new[]
			{
				UiTempXmlFile.Member("T:" + described, "<summary>XML service.</summary>"),
				UiTempXmlFile.Member(
					"M:" + described + ".Add(System.Int32,System.Int32)",
					"<summary>XML adds.</summary>",
					"<param name=\"left\">XML left.</param>",
					"<param name=\"right\">XML right.</param>"),
				UiTempXmlFile.Member(
					"M:" + described + ".Plain(System.String)",
					"<summary>XML plain.</summary>",
					"<param name=\"text\">XML text.</param>"),
			};

			return UiTempXmlFile.CreateDoc(members);
		}

		private static void AssertOperation(JsonElement service, string name, string summary, params string[] parameters)
		{
			var operation = UiTestHost.GetOperation(service, name);

			Assert.AreEqual(summary, DescriptionOf(operation), name);
			CollectionAssert.AreEqual(parameters, Parameters(operation), name);
		}

		private static string DescriptionOf(JsonElement element)
		{
			var description = element.GetProperty("description");
			return description.ValueKind == JsonValueKind.Null ? null : description.GetString();
		}

		// "name:description" for each parameter, the description being empty when it is null.
		private static string[] Parameters(JsonElement operation)
		{
			return operation.GetProperty("parameters").EnumerateArray()
				.Select(parameter => parameter.GetProperty("name").GetString() + ":" + (DescriptionOf(parameter) ?? string.Empty))
				.ToArray();
		}

		private static string[] Warnings(ListLoggerProvider logs)
		{
			return logs.Entries
				.Where(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(WarningText, StringComparison.Ordinal))
				.Select(entry => entry.Message)
				.ToArray();
		}
	}
}
