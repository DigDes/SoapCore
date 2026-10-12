using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// GET {pattern}/api/services: how the options added in phase 2 reach the JSON of the explorer.
	[TestClass]
	public class SoapUiCatalogOptionsTests
	{
		[TestMethod]
		public async Task Catalog_WithDefaultOptions_KeepsTodaysBehaviour()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			var root = document.RootElement;

			// The keys are always there: a missing option is null (or the default), never a missing key.
			Assert.AreEqual(JsonValueKind.Null, root.GetProperty("description").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, root.GetProperty("defaultSoapVersion").ValueKind);
			Assert.AreEqual("list", root.GetProperty("docExpansion").GetString());
			CollectionAssert.AreEqual(new[] { "bearer", "basic", "apiKey", "wsUsernameToken" }, UiTestHost.Strings(root.GetProperty("authSchemes")));
			Assert.AreEqual("X-Api-Key", root.GetProperty("apiKeyHeaderName").GetString());
			Assert.AreEqual(JsonValueKind.Null, root.GetProperty("stylesheetUrl").ValueKind);
			Assert.IsTrue(root.GetProperty("tryItOutEnabled").GetBoolean());
			Assert.IsFalse(root.GetProperty("operationDetailsEnabled").GetBoolean());
		}

		[TestMethod]
		public async Task Catalog_WithEveryOptionSet_ReportsThem()
		{
			using var host = UiTestHost.CreateWithUi(
				null,
				options =>
				{
					options.Title = "Weather explorer";
					options.Description = "Try the weather services of the team.";
					options.DefaultSoapVersion = "1.2";
					options.DocExpansion = SoapUiDocExpansion.Full;
					options.AuthSchemes = SoapUiAuthSchemes.Bearer | SoapUiAuthSchemes.ApiKey;
					options.ApiKeyHeaderName = "X-Weather-Key";
					options.StylesheetUrl = "/branding/explorer.css?v=7";
				});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			var root = document.RootElement;

			Assert.AreEqual("Weather explorer", root.GetProperty("title").GetString());
			Assert.AreEqual("Try the weather services of the team.", root.GetProperty("description").GetString());
			Assert.AreEqual("1.2", root.GetProperty("defaultSoapVersion").GetString());
			Assert.AreEqual("full", root.GetProperty("docExpansion").GetString());
			CollectionAssert.AreEqual(new[] { "bearer", "apiKey" }, UiTestHost.Strings(root.GetProperty("authSchemes")));
			Assert.AreEqual("X-Weather-Key", root.GetProperty("apiKeyHeaderName").GetString());
			Assert.AreEqual("/branding/explorer.css?v=7", root.GetProperty("stylesheetUrl").GetString());
		}

		[TestMethod]
		[DataRow(SoapUiAuthSchemes.None, "")]
		[DataRow(SoapUiAuthSchemes.Bearer, "bearer")]
		[DataRow(SoapUiAuthSchemes.Basic, "basic")]
		[DataRow(SoapUiAuthSchemes.ApiKey, "apiKey")]
		[DataRow(SoapUiAuthSchemes.WsUsernameToken, "wsUsernameToken")]
		[DataRow(SoapUiAuthSchemes.ApiKey | SoapUiAuthSchemes.Bearer, "bearer,apiKey")]
		[DataRow(SoapUiAuthSchemes.WsUsernameToken | SoapUiAuthSchemes.Basic, "basic,wsUsernameToken")]
		[DataRow(SoapUiAuthSchemes.Bearer | SoapUiAuthSchemes.Basic | SoapUiAuthSchemes.WsUsernameToken, "bearer,basic,wsUsernameToken")]
		[DataRow(SoapUiAuthSchemes.All, "bearer,basic,apiKey,wsUsernameToken")]
		public async Task AuthSchemes_AreListedInAFixedOrder_AndNoneIsAnEmptyArray(SoapUiAuthSchemes schemes, string expected)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.AuthSchemes = schemes);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var list = document.RootElement.GetProperty("authSchemes");
			Assert.AreEqual(JsonValueKind.Array, list.ValueKind);
			Assert.AreEqual(expected, string.Join(",", UiTestHost.Strings(list)));
		}

		[TestMethod]
		[DataRow(SoapUiDocExpansion.List, "list")]
		[DataRow(SoapUiDocExpansion.Full, "full")]
		public async Task DocExpansion_IsServedAsLowerCaseText(SoapUiDocExpansion value, string expected)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.DocExpansion = value);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(expected, document.RootElement.GetProperty("docExpansion").GetString());
		}

		[TestMethod]
		[DataRow("1.1")]
		[DataRow("1.2")]
		public async Task DefaultSoapVersion_IsServedAsConfigured(string configured)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.DefaultSoapVersion = configured);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(configured, document.RootElement.GetProperty("defaultSoapVersion").GetString());
		}

		[TestMethod]
		public async Task DefaultSoapVersion_NullOrEmpty_MeansAutomatic()
		{
			using var withNull = UiTestHost.CreateWithUi(null, options => options.DefaultSoapVersion = null);
			using var withEmpty = UiTestHost.CreateWithUi(null, options => options.DefaultSoapVersion = string.Empty);

			using var first = await UiTestHost.GetJsonAsync(withNull, UiTestHost.ApiPath);
			using var second = await UiTestHost.GetJsonAsync(withEmpty, UiTestHost.ApiPath);

			Assert.AreEqual(JsonValueKind.Null, first.RootElement.GetProperty("defaultSoapVersion").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, second.RootElement.GetProperty("defaultSoapVersion").ValueKind);
		}

		[TestMethod]
		[DataRow("")]
		[DataRow("   ")]
		public async Task Description_EmptyOrWhitespace_IsNull(string description)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.Description = description);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty("description").ValueKind);
		}

		[TestMethod]
		public async Task Description_Null_IsNull()
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.Description = null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty("description").ValueKind);
		}

		[TestMethod]
		public async Task Description_IsPlainText_AndTheJsonNeverCarriesMarkup()
		{
			const string text = "Weather <b>team</b> & \"friends\" <script>alert(1)</script>";
			using var host = UiTestHost.CreateWithUi(null, options => options.Description = text);

			var body = await UiTestHost.GetTextAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(body);

			Assert.AreEqual(text, document.RootElement.GetProperty("description").GetString(), "The text is served as it is, as text.");
			Assert.IsFalse(body.Contains('<'), "The JSON must not contain a raw '<'.");
			Assert.IsFalse(body.Contains('>'), "The JSON must not contain a raw '>'.");
			Assert.IsFalse(body.Contains('&'), "The JSON must not contain a raw '&'.");
		}

		[TestMethod]
		public async Task StylesheetUrl_IsServedAsConfigured_NotCombinedWithThePathBase()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(options => options.StylesheetUrl = "/branding.css"),
				app =>
				{
					app.UsePathBase("/base");
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});

			using var document = await UiTestHost.GetJsonAsync(host, "/base/soap-ui/api/services");

			Assert.AreEqual("/branding.css", document.RootElement.GetProperty("stylesheetUrl").GetString());
			Assert.AreEqual("/base", document.RootElement.GetProperty("basePath").GetString());
		}

		[TestMethod]
		public async Task Options_AreReadOnEveryRequest()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var liveOptions = host.Services.GetRequiredService<IOptions<SoapUiOptions>>().Value;

			using (var before = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath))
			{
				Assert.AreEqual("list", before.RootElement.GetProperty("docExpansion").GetString());
				Assert.AreEqual(4, before.RootElement.GetProperty("authSchemes").GetArrayLength());
			}

			liveOptions.DocExpansion = SoapUiDocExpansion.Full;
			liveOptions.AuthSchemes = SoapUiAuthSchemes.Basic;
			liveOptions.Description = "Changed";

			using var after = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			Assert.AreEqual("full", after.RootElement.GetProperty("docExpansion").GetString());
			CollectionAssert.AreEqual(new[] { "basic" }, UiTestHost.Strings(after.RootElement.GetProperty("authSchemes")));
			Assert.AreEqual("Changed", after.RootElement.GetProperty("description").GetString());
		}

		[TestMethod]
		public async Task Options_ChangedAfterValidationToInvalidValues_AreNotServed()
		{
			// Only a change made after the options were validated can get here. The values that the page acts on are checked again on the way out.
			using var host = UiTestHost.CreateWithUi(null);
			var liveOptions = host.Services.GetRequiredService<IOptions<SoapUiOptions>>().Value;
			liveOptions.StylesheetUrl = "https://evil.example/x.css";
			liveOptions.DefaultSoapVersion = "9.9";
			liveOptions.DocExpansion = (SoapUiDocExpansion)42;

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			var root = document.RootElement;

			Assert.AreEqual(JsonValueKind.Null, root.GetProperty("stylesheetUrl").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, root.GetProperty("defaultSoapVersion").ValueKind);
			Assert.AreEqual("list", root.GetProperty("docExpansion").GetString());
		}

		[TestMethod]
		public async Task Descriptions_WhenNothingDescribesAService_AreNullButPresent()
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.ExposeOperationDetails = true);
			UiTestHost.Register(host, "/Details.svc", typeof(IUiDetailsService));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var service = UiTestHost.GetService(document, 0);
			Assert.AreEqual(JsonValueKind.Null, service.GetProperty("description").ValueKind);
			foreach (var operation in service.GetProperty("operations").EnumerateArray())
			{
				Assert.AreEqual(JsonValueKind.Null, operation.GetProperty("description").ValueKind, operation.GetProperty("name").GetString());
				foreach (var parameter in operation.GetProperty("parameters").EnumerateArray())
				{
					Assert.AreEqual(JsonValueKind.Null, parameter.GetProperty("description").ValueKind, operation.GetProperty("name").GetString());
				}
			}

			Assert.IsTrue(service.GetProperty("operations").EnumerateArray().Any(), "The operations are still listed.");
		}

		[TestMethod]
		public async Task Descriptions_OfAServiceThatCannotBeDescribed_AreNull()
		{
			using var host = UiTestHost.CreateWithUi(null);
			UiTestHost.Register(host, "/Broken.svc", null);
			UiTestHost.Register(host, "/Failing.svc", typeof(UiBrokenService));

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			foreach (var index in new[] { 0, 1 })
			{
				Assert.AreEqual(JsonValueKind.Null, UiTestHost.GetService(document, index).GetProperty("description").ValueKind);
			}
		}
	}
}
