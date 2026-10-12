using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// The options added in phase 2 (Description, DefaultSoapVersion, DocExpansion, AuthSchemes, ApiKeyHeaderName, StylesheetUrl,
	// UseDescriptionAttributes, IncludeXmlComments): defaults, the fluent IncludeXmlComments method, and validation.
	[TestClass]
	public class SoapUiOptionsTests
	{
		[TestMethod]
		public void Defaults_KeepTodaysBehaviour()
		{
			var options = new SoapUiOptions();

			Assert.IsNull(options.Description);
			Assert.IsNull(options.DefaultSoapVersion);
			Assert.AreEqual(SoapUiDocExpansion.List, options.DocExpansion);
			Assert.AreEqual(SoapUiAuthSchemes.All, options.AuthSchemes);
			Assert.AreEqual("X-Api-Key", options.ApiKeyHeaderName);
			Assert.IsNull(options.StylesheetUrl);
			Assert.IsTrue(options.UseDescriptionAttributes);
			Assert.AreEqual(0, options.XmlCommentsFiles.Count);

			// Untouched by phase 2.
			Assert.AreEqual("SoapCore Explorer", options.Title);
			Assert.IsTrue(options.EnableTryItOut);
			Assert.IsFalse(options.ExposeOperationDetails);
			Assert.AreEqual(string.Empty, options.EndpointPathPrefix);
		}

		[TestMethod]
		public void DocExpansion_HasListAsTheDefaultAndFull()
		{
			CollectionAssert.AreEquivalent(new[] { "List", "Full" }, Enum.GetNames(typeof(SoapUiDocExpansion)));
			Assert.AreEqual(0, (int)SoapUiDocExpansion.List, "List is the default(T) value.");
		}

		[TestMethod]
		public void AuthSchemes_AreFlagsAndAllIsTheirCombination()
		{
			Assert.AreEqual(0, (int)SoapUiAuthSchemes.None);
			Assert.AreEqual(1, (int)SoapUiAuthSchemes.Bearer);
			Assert.AreEqual(2, (int)SoapUiAuthSchemes.Basic);
			Assert.AreEqual(4, (int)SoapUiAuthSchemes.ApiKey);
			Assert.AreEqual(8, (int)SoapUiAuthSchemes.WsUsernameToken);
			Assert.AreEqual(15, (int)SoapUiAuthSchemes.All);
			Assert.AreEqual(SoapUiAuthSchemes.Bearer | SoapUiAuthSchemes.Basic | SoapUiAuthSchemes.ApiKey | SoapUiAuthSchemes.WsUsernameToken, SoapUiAuthSchemes.All);
		}

		[TestMethod]
		public void IncludeXmlComments_ReturnsTheSameOptionsAndKeepsTheOrder()
		{
			var options = new SoapUiOptions();

			var result = options.IncludeXmlComments("first.xml").IncludeXmlComments("second.xml");

			Assert.AreSame(options, result);
			CollectionAssert.AreEqual(new[] { "first.xml", "second.xml" }, options.XmlCommentsFiles.ToArray());
		}

		[TestMethod]
		public void IncludeXmlComments_SamePathTwice_IsListedOnce()
		{
			var options = new SoapUiOptions();

			options.IncludeXmlComments("docs.xml").IncludeXmlComments("docs.xml");

			CollectionAssert.AreEqual(new[] { "docs.xml" }, options.XmlCommentsFiles.ToArray());
		}

		[TestMethod]
		public void IncludeXmlComments_NullPath_ThrowsArgumentNullException()
		{
			var options = new SoapUiOptions();

			Assert.ThrowsException<ArgumentNullException>(() => options.IncludeXmlComments(null));
		}

		[TestMethod]
		[DataRow("")]
		[DataRow(" ")]
		[DataRow("\t")]
		[DataRow("a\0b")]
		public void IncludeXmlComments_EmptyOrInvalidPath_ThrowsArgumentException(string path)
		{
			var options = new SoapUiOptions();

			Assert.ThrowsException<ArgumentException>(() => options.IncludeXmlComments(path));
			Assert.AreEqual(0, options.XmlCommentsFiles.Count);
		}

		[TestMethod]
		public async Task IncludeXmlComments_AMissingFile_IsNotAnOptionsError()
		{
			// The file is only read when a service is described, and a problem with it never stops the explorer.
			using var host = UiTestHost.CreateWithUi(null, options => options.IncludeXmlComments(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "soapcore-ui-does-not-exist.xml")));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(0, document.RootElement.GetProperty("services").GetArrayLength());
		}

		[TestMethod]
		[DataRow("A plain subtitle")]
		[DataRow("Servicios de clima, version 2 - ñandú")]
		[DataRow("   ")]
		[DataRow("")]
		public async Task Description_Valid_IsAccepted(string description)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.Description = description);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
		}

		[TestMethod]
		public async Task Description_OfNullOr300Characters_IsAccepted()
		{
			using var withNull = UiTestHost.CreateWithUi(null, options => options.Description = null);
			using var longest = UiTestHost.CreateWithUi(null, options => options.Description = new string('d', 300));

			using var first = await UiTestHost.GetJsonAsync(withNull, UiTestHost.ApiPath);
			using var second = await UiTestHost.GetJsonAsync(longest, UiTestHost.ApiPath);

			Assert.AreEqual(JsonValueKind.Null, first.RootElement.GetProperty("description").ValueKind);
			Assert.AreEqual(new string('d', 300), second.RootElement.GetProperty("description").GetString());
		}

		[TestMethod]
		public void Description_LongerThan300Characters_ThrowsOptionsValidationException()
		{
			var exception = AssertInvalid(options => options.Description = new string('d', 301));

			StringAssert.Contains(Failures(exception), "Description");
			StringAssert.Contains(Failures(exception), "300");
		}

		[TestMethod]
		[DataRow("line\nbreak")]
		[DataRow("line\r\nbreak")]
		[DataRow("tab\there")]
		[DataRow("bell\u0007")]
		[DataRow("nul\0")]
		public void Description_WithControlCharacters_ThrowsOptionsValidationException(string description)
		{
			var exception = AssertInvalid(options => options.Description = description);

			StringAssert.Contains(Failures(exception), "Description");
			StringAssert.Contains(Failures(exception), "control characters");
		}

		[TestMethod]
		[DataRow("1.1")]
		[DataRow("1.2")]
		[DataRow("")]
		public async Task DefaultSoapVersion_Valid_IsAccepted(string version)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.DefaultSoapVersion = version);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
		}

		[TestMethod]
		[DataRow("1.0")]
		[DataRow("2.0")]
		[DataRow("1")]
		[DataRow("1.1 ")]
		[DataRow(" 1.2")]
		[DataRow(" ")]
		[DataRow("soap12")]
		[DataRow("1.2.0")]
		[DataRow("1,1")]
		[DataRow("1.1\n")]
		public void DefaultSoapVersion_Invalid_ThrowsOptionsValidationException(string version)
		{
			var exception = AssertInvalid(options => options.DefaultSoapVersion = version);

			StringAssert.Contains(Failures(exception), "DefaultSoapVersion");
		}

		[TestMethod]
		[DataRow((SoapUiDocExpansion)2)]
		[DataRow((SoapUiDocExpansion)99)]
		[DataRow((SoapUiDocExpansion)(-1))]
		public void DocExpansion_NotAnEnumValue_ThrowsOptionsValidationException(SoapUiDocExpansion value)
		{
			var exception = AssertInvalid(options => options.DocExpansion = value);

			StringAssert.Contains(Failures(exception), "DocExpansion");
		}

		[TestMethod]
		[DataRow(SoapUiDocExpansion.List)]
		[DataRow(SoapUiDocExpansion.Full)]
		public async Task DocExpansion_Valid_IsAccepted(SoapUiDocExpansion value)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.DocExpansion = value);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
		}

		[TestMethod]
		[DataRow((SoapUiAuthSchemes)16)]
		[DataRow((SoapUiAuthSchemes)31)]
		[DataRow((SoapUiAuthSchemes)255)]
		[DataRow((SoapUiAuthSchemes)(-1))]
		public void AuthSchemes_WithBitsThatAreNotSchemes_ThrowsOptionsValidationException(SoapUiAuthSchemes value)
		{
			var exception = AssertInvalid(options => options.AuthSchemes = value);

			StringAssert.Contains(Failures(exception), "AuthSchemes");
		}

		[TestMethod]
		[DataRow(SoapUiAuthSchemes.None)]
		[DataRow(SoapUiAuthSchemes.Bearer)]
		[DataRow(SoapUiAuthSchemes.Bearer | SoapUiAuthSchemes.Basic)]
		[DataRow(SoapUiAuthSchemes.ApiKey | SoapUiAuthSchemes.WsUsernameToken)]
		[DataRow(SoapUiAuthSchemes.All)]
		public async Task AuthSchemes_Valid_IsAccepted(SoapUiAuthSchemes value)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.AuthSchemes = value);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
		}

		[TestMethod]
		[DataRow("X-Api-Key")]
		[DataRow("Api_Key")]
		[DataRow("x-auth-token")]
		[DataRow("A")]
		[DataRow("Authorization")]
		[DataRow("X-Sec-Key")]
		[DataRow("Secure")]
		[DataRow("Proxy")]
		public async Task ApiKeyHeaderName_Valid_IsAccepted(string name)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.ApiKeyHeaderName = name);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(name, document.RootElement.GetProperty("apiKeyHeaderName").GetString());
		}

		[TestMethod]
		public async Task ApiKeyHeaderName_Of128Characters_IsAccepted()
		{
			var name = new string('K', 128);
			using var host = UiTestHost.CreateWithUi(null, options => options.ApiKeyHeaderName = name);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(name, document.RootElement.GetProperty("apiKeyHeaderName").GetString());
		}

		[TestMethod]
		[DataRow("")]
		[DataRow(" ")]
		[DataRow("X Api Key")]
		[DataRow("X-Api-Key ")]
		[DataRow("X-Api-Key\n")]
		[DataRow("X-Api-Key\r\n")]
		[DataRow("X:Key")]
		[DataRow("X.Key")]
		[DataRow("Clé")]
		[DataRow("X-Key;")]
		public void ApiKeyHeaderName_NotAHeaderName_ThrowsOptionsValidationException(string name)
		{
			var exception = AssertInvalid(options => options.ApiKeyHeaderName = name);

			StringAssert.Contains(Failures(exception), "ApiKeyHeaderName");
		}

		[TestMethod]
		public void ApiKeyHeaderName_Null_ThrowsOptionsValidationException()
		{
			var exception = AssertInvalid(options => options.ApiKeyHeaderName = null);

			StringAssert.Contains(Failures(exception), "ApiKeyHeaderName");
		}

		[TestMethod]
		public void ApiKeyHeaderName_Of129Characters_ThrowsOptionsValidationException()
		{
			var exception = AssertInvalid(options => options.ApiKeyHeaderName = new string('K', 129));

			StringAssert.Contains(Failures(exception), "ApiKeyHeaderName");
			StringAssert.Contains(Failures(exception), "128");
		}

		[TestMethod]
		[DataRow("Content-Type")]
		[DataRow("content-type")]
		[DataRow("SOAPAction")]
		[DataRow("soapaction")]
		public void ApiKeyHeaderName_OwnedByTheExplorer_ThrowsOptionsValidationException(string name)
		{
			var exception = AssertInvalid(options => options.ApiKeyHeaderName = name);

			StringAssert.Contains(Failures(exception), "ApiKeyHeaderName");
			StringAssert.Contains(Failures(exception), "the explorer sets that header itself");
		}

		[TestMethod]
		[DataRow("Host")]
		[DataRow("HOST")]
		[DataRow("Content-Length")]
		[DataRow("Connection")]
		[DataRow("Cookie")]
		[DataRow("Cookie2")]
		[DataRow("Origin")]
		[DataRow("Referer")]
		[DataRow("Date")]
		[DataRow("Expect")]
		[DataRow("Keep-Alive")]
		[DataRow("TE")]
		[DataRow("Trailer")]
		[DataRow("Transfer-Encoding")]
		[DataRow("Upgrade")]
		[DataRow("Via")]
		[DataRow("DNT")]
		[DataRow("Accept-Charset")]
		[DataRow("Accept-Encoding")]
		[DataRow("Proxy-Authorization")]
		[DataRow("proxy-x")]
		[DataRow("Sec-Fetch-Mode")]
		[DataRow("SEC-X")]
		[DataRow("Access-Control-Request-Method")]
		[DataRow("Access-Control-Request-Headers")]
		public void ApiKeyHeaderName_ForbiddenInBrowsers_ThrowsOptionsValidationException(string name)
		{
			var exception = AssertInvalid(options => options.ApiKeyHeaderName = name);

			StringAssert.Contains(Failures(exception), "ApiKeyHeaderName");
			StringAssert.Contains(Failures(exception), "browsers do not let a page set that header");
		}

		[TestMethod]
		[DataRow("/branding.css")]
		[DataRow("/css/site.css")]
		[DataRow("/static/branding.css?v=3")]
		[DataRow("/a/b-c_d.e~f/%20x.css?x=1&y=2")]
		[DataRow("/branding.css?")]
		[DataRow("/a")]
		public async Task StylesheetUrl_ASameOriginPath_IsAccepted(string url)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.StylesheetUrl = url);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(url, document.RootElement.GetProperty("stylesheetUrl").GetString());
		}

		[TestMethod]
		public async Task StylesheetUrl_NullOrEmpty_MeansNone()
		{
			using var withNull = UiTestHost.CreateWithUi(null, options => options.StylesheetUrl = null);
			using var withEmpty = UiTestHost.CreateWithUi(null, options => options.StylesheetUrl = string.Empty);

			using var first = await UiTestHost.GetJsonAsync(withNull, UiTestHost.ApiPath);
			using var second = await UiTestHost.GetJsonAsync(withEmpty, UiTestHost.ApiPath);

			Assert.AreEqual(JsonValueKind.Null, first.RootElement.GetProperty("stylesheetUrl").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, second.RootElement.GetProperty("stylesheetUrl").ValueKind);
		}

		[TestMethod]
		public async Task StylesheetUrl_Of200Characters_IsAccepted()
		{
			var url = "/" + new string('s', 199);
			using var host = UiTestHost.CreateWithUi(null, options => options.StylesheetUrl = url);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(url, document.RootElement.GetProperty("stylesheetUrl").GetString());
		}

		[TestMethod]
		[DataRow("https://evil.example/x.css")]
		[DataRow("http://evil.example/x.css")]
		[DataRow("//evil.example/x.css")]
		[DataRow("///evil.example/x.css")]
		[DataRow("/\\evil.example/x.css")]
		[DataRow("\\\\evil.example\\x.css")]
		[DataRow("javascript:alert(1)")]
		[DataRow("data:text/css,body{}")]
		[DataRow("file:///etc/passwd")]
		[DataRow("branding.css")]
		[DataRow("./branding.css")]
		[DataRow("../branding.css")]
		[DataRow(" /branding.css")]
		[DataRow("/branding.css ")]
		[DataRow("/brand ing.css")]
		[DataRow("/brand\ting.css")]
		[DataRow("/branding.css\n")]
		[DataRow("/branding.css\r\n")]
		[DataRow("/branding.css#fragment")]
		[DataRow("/branding.css?v=1#fragment")]
		[DataRow("/branding.css?a?b")]
		[DataRow("/branding.css;x")]
		[DataRow("/<script>")]
		[DataRow("/x\"y.css")]
		[DataRow("/x'y.css")]
		[DataRow("/x(y).css")]
		[DataRow("/")]
		[DataRow(" ")]
		public void StylesheetUrl_NotASameOriginPath_ThrowsOptionsValidationException(string url)
		{
			var exception = AssertInvalid(options => options.StylesheetUrl = url);

			StringAssert.Contains(Failures(exception), "StylesheetUrl");
		}

		[TestMethod]
		public void StylesheetUrl_Of201Characters_ThrowsOptionsValidationException()
		{
			var exception = AssertInvalid(options => options.StylesheetUrl = "/" + new string('s', 200));

			StringAssert.Contains(Failures(exception), "StylesheetUrl");
		}

		[TestMethod]
		public void Validation_ReportsEveryProblemAtOnce()
		{
			var exception = AssertInvalid(options =>
			{
				options.Description = "bad\nline";
				options.DefaultSoapVersion = "3.0";
				options.ApiKeyHeaderName = "Cookie";
				options.StylesheetUrl = "https://evil.example/x.css";
				options.AuthSchemes = (SoapUiAuthSchemes)64;
				options.DocExpansion = (SoapUiDocExpansion)7;
			});

			Assert.AreEqual(6, exception.Failures.Count());
			var text = Failures(exception);
			foreach (var name in new[] { "Description", "DefaultSoapVersion", "ApiKeyHeaderName", "StylesheetUrl", "AuthSchemes", "DocExpansion" })
			{
				StringAssert.Contains(text, name);
			}
		}

		[TestMethod]
		public void UseDescriptionAttributes_BothValues_AreAccepted()
		{
			using var enabled = UiTestHost.CreateWithUi(null, options => options.UseDescriptionAttributes = true);
			using var disabled = UiTestHost.CreateWithUi(null, options => options.UseDescriptionAttributes = false);

			Assert.IsNotNull(enabled);
			Assert.IsNotNull(disabled);
		}

		private static OptionsValidationException AssertInvalid(Action<SoapUiOptions> configure)
		{
			return Assert.ThrowsException<OptionsValidationException>(() => UiTestHost.CreateWithUi(null, configure));
		}

		private static string Failures(OptionsValidationException exception)
		{
			return string.Join(" | ", exception.Failures);
		}
	}
}
