using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// The options added in phase 3 (EnableFormEditor, SendTimeoutSeconds, MaxResponseBytes) and the catalog members that carry them
	// (formEditorEnabled, sendTimeoutSeconds, maxResponseBytes, uiVersion): defaults, validation, and the JSON of the explorer.
	[TestClass]
	public class SoapUiPhase3OptionsTests
	{
		private const int DefaultSendTimeoutSeconds = 60;
		private const int DefaultMaxResponseBytes = 5242880;

		[TestMethod]
		public void Defaults_KeepTodaysBehaviour()
		{
			var options = new SoapUiOptions();

			Assert.IsTrue(options.EnableFormEditor);
			Assert.AreEqual(DefaultSendTimeoutSeconds, options.SendTimeoutSeconds);
			Assert.AreEqual(DefaultMaxResponseBytes, options.MaxResponseBytes);
			Assert.AreEqual(5 * 1024 * 1024, options.MaxResponseBytes, "The default is 5 MiB.");
		}

		[TestMethod]
		[DataRow(1)]
		[DataRow(2)]
		[DataRow(30)]
		[DataRow(60)]
		[DataRow(599)]
		[DataRow(600)]
		public async Task SendTimeoutSeconds_InRange_IsAcceptedAndServed(int seconds)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.SendTimeoutSeconds = seconds);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(seconds, document.RootElement.GetProperty("sendTimeoutSeconds").GetInt32());
		}

		[TestMethod]
		[DataRow(0)]
		[DataRow(-1)]
		[DataRow(601)]
		[DataRow(3600)]
		[DataRow(int.MinValue)]
		[DataRow(int.MaxValue)]
		public void SendTimeoutSeconds_OutOfRange_ThrowsOptionsValidationException(int seconds)
		{
			var exception = AssertInvalid(options => options.SendTimeoutSeconds = seconds);

			Assert.AreEqual(1, exception.Failures.Count());
			StringAssert.Contains(Failures(exception), "SendTimeoutSeconds");
			StringAssert.Contains(Failures(exception), "between 1 and 600");
		}

		[TestMethod]
		[DataRow(1024)]
		[DataRow(1025)]
		[DataRow(65536)]
		[DataRow(1048576)]
		[DataRow(5242880)]
		[DataRow(52428799)]
		[DataRow(52428800)]
		public async Task MaxResponseBytes_InRange_IsAcceptedAndServed(int bytes)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.MaxResponseBytes = bytes);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(bytes, document.RootElement.GetProperty("maxResponseBytes").GetInt32());
		}

		[TestMethod]
		[DataRow(0)]
		[DataRow(1)]
		[DataRow(1023)]
		[DataRow(-1)]
		[DataRow(52428801)]
		[DataRow(104857600)]
		[DataRow(int.MinValue)]
		[DataRow(int.MaxValue)]
		public void MaxResponseBytes_OutOfRange_ThrowsOptionsValidationException(int bytes)
		{
			var exception = AssertInvalid(options => options.MaxResponseBytes = bytes);

			Assert.AreEqual(1, exception.Failures.Count());
			StringAssert.Contains(Failures(exception), "MaxResponseBytes");
			StringAssert.Contains(Failures(exception), "between 1024 and 52428800");
		}

		[TestMethod]
		public void Validation_ReportsBothLimitsAtOnce_AlongsideOtherProblems()
		{
			var exception = AssertInvalid(options =>
			{
				options.SendTimeoutSeconds = 0;
				options.MaxResponseBytes = 10;
				options.DefaultSoapVersion = "3.0";
			});

			Assert.AreEqual(3, exception.Failures.Count());
			var text = Failures(exception);
			foreach (var name in new[] { "SendTimeoutSeconds", "MaxResponseBytes", "DefaultSoapVersion" })
			{
				StringAssert.Contains(text, name);
			}
		}

		[TestMethod]
		[DataRow(true)]
		[DataRow(false)]
		public async Task EnableFormEditor_BothValues_AreAcceptedAndServed(bool enabled)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.EnableFormEditor = enabled);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(enabled ? JsonValueKind.True : JsonValueKind.False, document.RootElement.GetProperty("formEditorEnabled").ValueKind);
		}

		[TestMethod]
		public async Task Catalog_WithDefaultOptions_ReportsTheNewMembers()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			var root = document.RootElement;

			Assert.AreEqual(JsonValueKind.True, root.GetProperty("formEditorEnabled").ValueKind);
			Assert.AreEqual(JsonValueKind.Number, root.GetProperty("sendTimeoutSeconds").ValueKind);
			Assert.AreEqual(DefaultSendTimeoutSeconds, root.GetProperty("sendTimeoutSeconds").GetInt32());
			Assert.AreEqual(JsonValueKind.Number, root.GetProperty("maxResponseBytes").ValueKind);
			Assert.AreEqual(DefaultMaxResponseBytes, root.GetProperty("maxResponseBytes").GetInt32());
			Assert.AreEqual(JsonValueKind.String, root.GetProperty("uiVersion").ValueKind);
			Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("uiVersion").GetString()));
		}

		[TestMethod]
		public async Task Catalog_WithConfiguredOptions_ReportsThem()
		{
			using var host = UiTestHost.CreateWithUi(
				null,
				options =>
				{
					options.EnableFormEditor = false;
					options.SendTimeoutSeconds = 15;
					options.MaxResponseBytes = 2097152;
				});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			var root = document.RootElement;

			Assert.AreEqual(JsonValueKind.False, root.GetProperty("formEditorEnabled").ValueKind);
			Assert.AreEqual(15, root.GetProperty("sendTimeoutSeconds").GetInt32());
			Assert.AreEqual(2097152, root.GetProperty("maxResponseBytes").GetInt32());
		}

		[TestMethod]
		public async Task Catalog_NewMembers_SitRightAfterStylesheetUrlAndServicesStaysLast()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			var names = UiTestHost.PropertyNames(document.RootElement);
			var start = Array.IndexOf(names, "stylesheetUrl");
			Assert.IsTrue(start >= 0, "stylesheetUrl is part of the catalog.");
			CollectionAssert.AreEqual(
				new[] { "stylesheetUrl", "formEditorEnabled", "sendTimeoutSeconds", "maxResponseBytes", "uiVersion", "services" },
				names.Skip(start).ToArray());
		}

		[TestMethod]
		public async Task UiVersion_IsAPlainVersionWithoutBuildMetadataOrPaths()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			var version = document.RootElement.GetProperty("uiVersion").GetString();

			Assert.IsFalse(string.IsNullOrWhiteSpace(version));
			Assert.IsFalse(version.Contains('+'), "Build metadata (the part after '+') must be cut: " + version);
			Assert.IsFalse(version.Contains('/'), "The version must not look like a path: " + version);
			Assert.IsFalse(version.Contains('\\'), "The version must not look like a path: " + version);
			Assert.IsFalse(version.Contains(':'), "The version must not look like a path: " + version);
			Assert.IsTrue(
				Regex.IsMatch(version, @"^[0-9A-Za-z][0-9A-Za-z._-]{0,63}\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(10)),
				"The version must only contain letters, digits, '.', '-' and '_': " + version);
		}

		[TestMethod]
		public async Task UiVersion_IsTheInformationalVersionOfTheAssemblyWithoutMetadata()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var assembly = typeof(SoapUiOptions).Assembly;
			var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			var expected = string.IsNullOrEmpty(informational) ? assembly.GetName().Version.ToString() : informational.Split('+')[0];

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(expected, document.RootElement.GetProperty("uiVersion").GetString());
		}

		[TestMethod]
		public async Task UiVersion_IsTheSameOnEveryRequestAndForEveryConfiguration()
		{
			using var plain = UiTestHost.CreateWithUi(null);
			using var configured = UiTestHost.CreateWithUi(
				null,
				options =>
				{
					options.Title = "Another title";
					options.EnableFormEditor = false;
					options.SendTimeoutSeconds = 5;
				});

			using var first = await UiTestHost.GetJsonAsync(plain, UiTestHost.ApiPath);
			using var second = await UiTestHost.GetJsonAsync(plain, UiTestHost.ApiPath);
			using var third = await UiTestHost.GetJsonAsync(configured, UiTestHost.ApiPath);

			var version = first.RootElement.GetProperty("uiVersion").GetString();
			Assert.AreEqual(version, second.RootElement.GetProperty("uiVersion").GetString());
			Assert.AreEqual(version, third.RootElement.GetProperty("uiVersion").GetString());
		}

		[TestMethod]
		public async Task Options_AreReadOnEveryRequest()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var liveOptions = host.Services.GetRequiredService<IOptions<SoapUiOptions>>().Value;

			using (var before = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath))
			{
				Assert.AreEqual(JsonValueKind.True, before.RootElement.GetProperty("formEditorEnabled").ValueKind);
				Assert.AreEqual(DefaultSendTimeoutSeconds, before.RootElement.GetProperty("sendTimeoutSeconds").GetInt32());
				Assert.AreEqual(DefaultMaxResponseBytes, before.RootElement.GetProperty("maxResponseBytes").GetInt32());
			}

			liveOptions.EnableFormEditor = false;
			liveOptions.SendTimeoutSeconds = 5;
			liveOptions.MaxResponseBytes = 2048;

			using var after = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			Assert.AreEqual(JsonValueKind.False, after.RootElement.GetProperty("formEditorEnabled").ValueKind);
			Assert.AreEqual(5, after.RootElement.GetProperty("sendTimeoutSeconds").GetInt32());
			Assert.AreEqual(2048, after.RootElement.GetProperty("maxResponseBytes").GetInt32());
		}

		[TestMethod]
		[DataRow(0)]
		[DataRow(-5)]
		[DataRow(601)]
		[DataRow(int.MaxValue)]
		public async Task Options_ChangedAfterValidationToAnInvalidTimeout_ServeTheDefault(int seconds)
		{
			// Only a change made after the options were validated can get here. The limits that the page acts on are checked again on the way out.
			using var host = UiTestHost.CreateWithUi(null);
			host.Services.GetRequiredService<IOptions<SoapUiOptions>>().Value.SendTimeoutSeconds = seconds;

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(DefaultSendTimeoutSeconds, document.RootElement.GetProperty("sendTimeoutSeconds").GetInt32());
		}

		[TestMethod]
		[DataRow(0)]
		[DataRow(-5)]
		[DataRow(1023)]
		[DataRow(52428801)]
		[DataRow(int.MaxValue)]
		public async Task Options_ChangedAfterValidationToAnInvalidResponseLimit_ServeTheDefault(int bytes)
		{
			using var host = UiTestHost.CreateWithUi(null);
			host.Services.GetRequiredService<IOptions<SoapUiOptions>>().Value.MaxResponseBytes = bytes;

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(DefaultMaxResponseBytes, document.RootElement.GetProperty("maxResponseBytes").GetInt32());
		}

		[TestMethod]
		public async Task NewMembers_DoNotChangeTheOtherMembersOfTheCatalog()
		{
			using var host = UiTestHost.CreateWithUi(UiTestHost.MapDefaultSoapEndpoints, options => options.EnableFormEditor = false);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);
			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			var root = document.RootElement;

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual("SoapCore Explorer", root.GetProperty("title").GetString());
			Assert.IsTrue(root.GetProperty("tryItOutEnabled").GetBoolean());
			Assert.AreEqual("list", root.GetProperty("docExpansion").GetString());
			Assert.AreEqual(2, root.GetProperty("services").GetArrayLength());
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
