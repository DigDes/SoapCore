using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.Ui
{
	// Lint-style checks over the embedded front end, fetched over HTTP like a browser would.
	// These are not proof of safety, they stop the well-known dangerous constructs from creeping back in.
	// Comments are scanned too, so the banned words must not appear in them either.
	[TestClass]
	public class SoapUiEmbeddedContentTests
	{
		private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(10);

		private static readonly Dictionary<string, string> JsPatterns = new Dictionary<string, string>
		{
			{ "J1", @"\b(?:innerHTML|outerHTML)\b" },
			{ "J2", @"\binsertAdjacentHTML\b" },
			{ "J3", @"\bdocument\s*\.\s*write(?:ln)?\s*\(" },
			{ "J4", @"(?<![\w$])eval\s*\(" },
			{ "J5", @"\bnew\s+Function\b" },
			{ "J6", @"(?<![\w$])Function\s*\(" },
			{ "J7", @"\bset(?:Timeout|Interval)\s*\(\s*[""'`]" },
			{ "J8", @"\bsetAttribute\s*\(\s*[""'](?:style|on[a-z]+|srcdoc|formaction)[""']" },
			{ "J9", @"\bjavascript\s*:" },
			{ "J10", @"\bsrcdoc\b" },
			{ "J11", @"(?<![\w$.])import\s*\(" },
			{ "J12", @"\.\s*style\b" },
			{ "J13", @"\bcreateElement\s*\(\s*[""'](?:script|style|iframe|object|embed|link|base|form)[""']" },
			{ "J14", @"\b(?:localStorage|sessionStorage|indexedDB)\b|\bdocument\s*\.\s*cookie\b" },
			{ "J15", @"\bexecCommand\b" },
			{ "J16", @"<[a-zA-Z][^>]*\son[a-z]+\s*=" },
			{ "J17", @"\bXMLHttpRequest\b|\bWebSocket\b|\bEventSource\b|\bsendBeacon\b" },
			{ "J18", @"[""']text/html[""']|\bsetHTMLUnsafe\b" },
			{ "J19", @"\b(?:importNode|adoptNode|createContextualFragment|parseHTMLUnsafe|setHTML|setHTMLUnsafe|execScript)\b" },
			{ "J20", @"\bcreateElementNS\s*\([^)]*[""'](?:script|style|iframe|object|embed|link|base|form)[""']" },
			{ "J21", @"\b(?:window|globalThis|self)\s*\.\s*(?:eval|Function)\b" },
			{ "J22", @"\bconsole\s*\." },
		};

		// app.js may create exactly one <link> (the same-origin custom stylesheet) and store exactly one preference in localStorage (the theme).
		// Both exceptions are pinned by their own tests below, every other pattern still applies to app.js.
		private static readonly Dictionary<string, string> AppJsPatterns = JsPatterns.ToDictionary(
			pattern => pattern.Key,
			pattern => pattern.Key switch
			{
				"J13" => @"\bcreateElement\s*\(\s*[""'](?:script|style|iframe|object|embed|base|form)[""']",
				"J14" => @"\b(?:sessionStorage|indexedDB)\b|\bdocument\s*\.\s*cookie\b",
				_ => pattern.Value,
			});

		private static readonly Dictionary<string, string> HtmlPatterns = new Dictionary<string, string>
		{
			{ "H1", @"<style\b" },
			{ "H2", @"<[a-zA-Z][^>]*\sstyle\s*=" },
			{ "H3", @"<[a-zA-Z][^>]*\son[a-z]+\s*=" },
			{ "H4", @"<(?:iframe|object|embed|base|form)\b|http-equiv" },
			{ "H5", @"\b(?:src|href|action|data|poster|formaction)\s*=\s*[""'](?:https?:)?//" },
			{ "H7", @"\bjavascript\s*:" },
		};

		private static readonly Dictionary<string, string> CssPatterns = new Dictionary<string, string>
		{
			{ "C1", @"@import" },
			{ "C2", @"\burl\(\s*(?![""']?data:)" },
		};

		// Absolute URLs that may appear in the front end: XML namespaces and sample values, never anything that is fetched.
		private static readonly string[] AllowedUrlPrefixes = new[]
		{
			"http://schemas.xmlsoap.org/soap/envelope/",
			"http://www.w3.org/2003/05/soap-envelope",
			"http://www.w3.org/2001/XMLSchema",
			"http://schemas.xmlsoap.org/wsdl/",
			"http://www.w3.org/2000/xmlns/",
			"http://www.w3.org/XML/1998/namespace",
			"http://tempuri.org/",
			"http://www.w3.org/2000/svg",
			"http://schemas.microsoft.com/2003/10/Serialization/",
			"http://docs.oasis-open.org/wss/",
			"http://microsoft.com/wsdl/types/",
			"https://example.com",
			"http://example.com",
		};

		[TestMethod]
		public async Task Index_HasNoInlineScriptStyleOrHandlers()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var html = await UiTestHost.GetTextAsync(host, "/soap-ui/");

			AssertNoMatches("index.html", html, HtmlPatterns);

			// H6: every <script> has a src and an empty body.
			var scriptTags = Regex.Matches(html, @"<script\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
			var scripts = Regex.Matches(html, @"<script\b([^>]*)>([\s\S]*?)</script>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
			Assert.IsTrue(scriptTags.Count > 0, "index.html must load its scripts.");
			Assert.AreEqual(scriptTags.Count, scripts.Count, "Every <script> must be closed and well formed.");

			foreach (Match script in scripts)
			{
				Assert.IsTrue(string.IsNullOrWhiteSpace(script.Groups[2].Value), "Inline script body found: " + script.Groups[2].Value);
				StringAssert.Contains(script.Groups[1].Value, "src=", "A <script> without src: " + script.Value);
			}
		}

		[TestMethod]
		public async Task Index_DeclaresEnglishLanguageAndHeading()
		{
			using var host = UiTestHost.CreateWithUi(null);

			var html = await UiTestHost.GetTextAsync(host, "/soap-ui/");

			StringAssert.Contains(html, "lang=\"en\"");
			StringAssert.Contains(html, "<h1");
		}

		[TestMethod]
		[DataRow("app.js")]
		[DataRow("wsdl-parser.js")]
		[DataRow("form-editor.js")]
		[DataRow("xml-view.js")]
		public async Task JavaScript_DoesNotUseBannedConstructs(string file)
		{
			using var host = UiTestHost.CreateWithUi(null);

			var script = await UiTestHost.GetTextAsync(host, "/soap-ui/" + file);

			AssertNoMatches(file, script, file == "app.js" ? AppJsPatterns : JsPatterns);
			StringAssert.Contains(script, "'use strict'", file + " must run in strict mode.");
		}

		[TestMethod]
		public async Task AppJs_CreatesOnlyTheStylesheetLinkOnTheSameOrigin()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var script = await UiTestHost.GetTextAsync(host, "/soap-ui/app.js");
			var links = Regex.Matches(script, @"\bcreateElement\s*\(\s*[""']link[""']", RegexOptions.CultureInvariant, RegexTimeout);

			Assert.AreEqual(1, links.Count, "app.js may create exactly one <link> element, for the same-origin custom stylesheet.");

			// The element is only created after the URL passed the same-origin check.
			var before = script.Substring(Math.Max(0, links[0].Index - 1500), Math.Min(1500, links[0].Index));
			StringAssert.Contains(before, @"^\/(?!\/)", "The stylesheet URL must be checked to start with a single slash before the link is created.");
		}

		[TestMethod]
		public async Task AppJs_StoresOnlyTheThemePreferenceInLocalStorage()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var script = await UiTestHost.GetTextAsync(host, "/soap-ui/app.js");
			var parser = await UiTestHost.GetTextAsync(host, "/soap-ui/wsdl-parser.js");

			Assert.IsFalse(Regex.IsMatch(parser, @"\blocalStorage\b", RegexOptions.CultureInvariant, RegexTimeout), "wsdl-parser.js must not use localStorage.");
			StringAssert.Contains(script, "const THEME_KEY = 'soapcore-ui-theme';");

			var all = Regex.Matches(script, @"\blocalStorage\b", RegexOptions.CultureInvariant, RegexTimeout).Count;
			var allowed = Regex.Matches(script, @"\blocalStorage\s*.\s*(?:getItem|setItem|removeItem)\s*\(\s*THEME_KEY\b", RegexOptions.CultureInvariant, RegexTimeout).Count;

			Assert.IsTrue(all > 0, "The theme preference is expected to use localStorage.");
			Assert.AreEqual(all, allowed, "Every localStorage access must be getItem, setItem or removeItem on THEME_KEY only.");
		}

		[TestMethod]
		[DataRow("style.css")]
		[DataRow("form-editor.css")]
		public async Task Css_HasNoImportOrNonDataUrls(string file)
		{
			using var host = UiTestHost.CreateWithUi(null);

			var css = await UiTestHost.GetTextAsync(host, "/soap-ui/" + file);

			AssertNoMatches(file, css, CssPatterns);
		}

		[TestMethod]
		public async Task AllAbsoluteUrls_AreInAllowList()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var files = new Dictionary<string, string>
			{
				{ "index.html", await UiTestHost.GetTextAsync(host, "/soap-ui/") },
				{ "app.js", await UiTestHost.GetTextAsync(host, "/soap-ui/app.js") },
				{ "wsdl-parser.js", await UiTestHost.GetTextAsync(host, "/soap-ui/wsdl-parser.js") },
				{ "form-editor.js", await UiTestHost.GetTextAsync(host, "/soap-ui/form-editor.js") },
				{ "xml-view.js", await UiTestHost.GetTextAsync(host, "/soap-ui/xml-view.js") },
				{ "style.css", await UiTestHost.GetTextAsync(host, "/soap-ui/style.css") },
				{ "form-editor.css", await UiTestHost.GetTextAsync(host, "/soap-ui/form-editor.css") },
			};
			var urlPattern = new Regex("https?://[^\\s\"'`)<>]+", RegexOptions.CultureInvariant, RegexTimeout);

			foreach (var file in files)
			{
				foreach (Match match in urlPattern.Matches(file.Value))
				{
					var url = match.Value;
					Assert.IsTrue(
						AllowedUrlPrefixes.Any(prefix => url.StartsWith(prefix, StringComparison.Ordinal)),
						file.Key + ": absolute URL is not in the allow-list: " + url);
				}
			}
		}

		private static void AssertNoMatches(string file, string text, Dictionary<string, string> patterns)
		{
			foreach (var pattern in patterns)
			{
				var match = new Regex(pattern.Value, RegexOptions.CultureInvariant, RegexTimeout).Match(text);

				Assert.IsFalse(match.Success, file + ": banned construct " + pattern.Key + " found near '" + Snippet(text, match.Index) + "'");
			}
		}

		private static string Snippet(string text, int index)
		{
			var start = Math.Max(0, index - 20);
			var length = Math.Min(text.Length - start, 80);

			return text.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ');
		}
	}
}
