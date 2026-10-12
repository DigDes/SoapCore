using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// The routes of the explorer: what each URL answers, with which headers, and what is deliberately not there.
	[TestClass]
	public class SoapUiEndpointTests
	{
		private static readonly string[] StaticIds = new[]
		{
			"app-title",
			"service-search",
			"service-list",
			"status-bar",
			"empty-state",
			"headers-editor",
			"headers-rows",
			"headers-add",
			"service-view",
			"soap-version",
			"soap-version-11",
			"soap-version-12",
			"tab-operations",
			"tab-wsdl",
			"operations-list",
			"wsdl-view",
		};

		// Every embedded file the explorer serves besides the index.
		private static readonly string[] AssetPaths = new[]
		{
			"/soap-ui/app.js",
			"/soap-ui/wsdl-parser.js",
			"/soap-ui/form-editor.js",
			"/soap-ui/xml-view.js",
			"/soap-ui/style.css",
			"/soap-ui/form-editor.css",
		};

		[TestMethod]
		public async Task Root_WithoutTrailingSlash_Redirects308WithRelativeLocationAndQuery()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.GetRawAsync(host, "/soap-ui?a=1&b=2");

			Assert.AreEqual(HttpStatusCode.PermanentRedirect, response.StatusCode);
			Assert.AreEqual("soap-ui/?a=1&b=2", response.Headers.Location.OriginalString);
			Assert.IsFalse(response.Headers.Location.OriginalString.StartsWith('/'), "The Location must be relative.");
			UiTestHost.AssertSecurityHeaders(response, false);
		}

		[TestMethod]
		public async Task Root_WithPathBase_RedirectLocationIsRelative()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UsePathBase("/base");
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});

			using var response = await UiTestHost.GetRawAsync(host, "/base/soap-ui?q=1");

			Assert.AreEqual(HttpStatusCode.PermanentRedirect, response.StatusCode);
			Assert.AreEqual("soap-ui/?q=1", response.Headers.Location.OriginalString);
		}

		[TestMethod]
		[DataRow("/soap-ui?next=//evil.com")]
		[DataRow("/soap-ui?next=%2F%2Fevil.com")]
		public async Task Redirect_NeverProducesSchemeRelativeLocation(string rawPath)
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.GetRawAsync(host, rawPath);

			Assert.AreEqual(HttpStatusCode.PermanentRedirect, response.StatusCode);
			var location = response.Headers.Location.OriginalString;
			StringAssert.StartsWith(location, "soap-ui/");
			Assert.IsFalse(location.StartsWith('/'), "The Location must never start with '/': " + location);
		}

		[TestMethod]
		public async Task Root_WithTrailingSlash_ServesIndexHtml()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.GetRawAsync(host, "/soap-ui/");

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual("text/html", response.Content.Headers.ContentType.MediaType);
			Assert.AreEqual("utf-8", response.Content.Headers.ContentType.CharSet);
			UiTestHost.AssertSecurityHeaders(response, false);
			Assert.IsNotNull(response.Headers.ETag);
			Assert.IsTrue((await response.Content.ReadAsStringAsync()).Length > 0);
		}

		[TestMethod]
		public async Task Index_ReferencesAssetsRelativelyAndHasRequiredElements()
		{
			using var host = UiTestHost.CreateWithUi(null);

			var body = await UiTestHost.GetTextAsync(host, "/soap-ui/");

			StringAssert.Contains(body, "href=\"style.css\"");
			StringAssert.Contains(body, "href=\"form-editor.css\"");

			// Every script is deferred, so they run in document order: the parser first, then the form editor, and all of them before app.js.
			var parserTag = FindScriptTag(body, "wsdl-parser.js");
			var formTag = FindScriptTag(body, "form-editor.js");
			var xmlViewTag = FindScriptTag(body, "xml-view.js");
			var appTag = FindScriptTag(body, "app.js");
			Assert.IsTrue(parserTag.Success, "index.html must load wsdl-parser.js.");
			Assert.IsTrue(formTag.Success, "index.html must load form-editor.js.");
			Assert.IsTrue(xmlViewTag.Success, "index.html must load xml-view.js.");
			Assert.IsTrue(appTag.Success, "index.html must load app.js.");
			Assert.IsTrue(parserTag.Index < formTag.Index, "wsdl-parser.js must load before form-editor.js.");
			Assert.IsTrue(formTag.Index < appTag.Index, "form-editor.js must load before app.js.");
			Assert.IsTrue(xmlViewTag.Index < appTag.Index, "xml-view.js must load before app.js.");
			Assert.IsTrue(parserTag.Index < appTag.Index, "wsdl-parser.js must load before app.js.");

			foreach (var tag in new[] { parserTag, formTag, xmlViewTag, appTag })
			{
				Assert.IsTrue(Regex.IsMatch(tag.Value, @"\bdefer\b", RegexOptions.CultureInvariant), "The script must be deferred: " + tag.Value);
			}

			Assert.IsFalse(body.Contains("<base", StringComparison.OrdinalIgnoreCase), "A <base> element would break the relative asset URLs.");

			foreach (var id in StaticIds)
			{
				StringAssert.Contains(body, "id=\"" + id + "\"", "Missing element id: " + id);
			}
		}

		[TestMethod]
		public async Task Index_EveryReferencedAssetIsServed()
		{
			using var host = UiTestHost.CreateWithUi(null);

			var body = await UiTestHost.GetTextAsync(host, "/soap-ui/");

			// Whatever index.html loads (scripts and style sheets) must be one of the whitelisted routes, so a new file cannot be forgotten.
			var references = Regex.Matches(body, "\\b(?:src|href)=\"(?<url>[^\"]+)\"", RegexOptions.CultureInvariant)
				.Cast<Match>()
				.Select(match => match.Groups["url"].Value)
				.Where(url => !url.StartsWith("data:", StringComparison.Ordinal) && !url.StartsWith('#'))
				.ToArray();

			Assert.IsTrue(references.Length >= 6, "Expected the style sheets and scripts of the page, found " + references.Length + ".");
			foreach (var reference in references)
			{
				Assert.IsFalse(reference.StartsWith('/') || reference.Contains(':', StringComparison.Ordinal), "The reference must be relative to the explorer: " + reference);

				using var response = await UiTestHost.GetRawAsync(host, "/soap-ui/" + reference);

				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, reference);
				UiTestHost.AssertSecurityHeaders(response, false);
			}
		}

		[TestMethod]
		public async Task EveryAsset_IsServedWithItsOwnContent()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var etags = new HashSet<string>(StringComparer.Ordinal);

			foreach (var path in AssetPaths.Concat(new[] { "/soap-ui/" }))
			{
				using var response = await UiTestHost.GetRawAsync(host, path);

				Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, path);
				Assert.IsNotNull(response.Headers.ETag, path);
				Assert.IsTrue(etags.Add(response.Headers.ETag.Tag), "Two routes serve the same content (same ETag): " + path);
			}

			Assert.AreEqual(AssetPaths.Length + 1, etags.Count);
		}

		[TestMethod]
		public async Task EveryAsset_IsServedOnlyOverGetAndHead()
		{
			using var host = UiTestHost.CreateWithUi(null);

			foreach (var path in AssetPaths)
			{
				using var get = await UiTestHost.GetRawAsync(host, path);
				using var head = await UiTestHost.SendAsync(host, HttpMethod.Head, path);
				using var post = await UiTestHost.SendAsync(host, HttpMethod.Post, path);

				Assert.AreEqual(HttpStatusCode.OK, get.StatusCode, "GET " + path);
				Assert.AreEqual(HttpStatusCode.OK, head.StatusCode, "HEAD " + path);
				Assert.AreEqual(0, (await head.Content.ReadAsByteArrayAsync()).Length, "HEAD has no body: " + path);
				Assert.AreEqual(HttpStatusCode.MethodNotAllowed, post.StatusCode, "POST " + path);
				UiTestHost.AssertSecurityHeaders(head, false);
			}
		}

		[TestMethod]
		public async Task Index_ContainsEveryElementIdThatAppJsRequires()
		{
			using var host = UiTestHost.CreateWithUi(null);

			var index = await UiTestHost.GetTextAsync(host, "/soap-ui/");
			var script = await UiTestHost.GetTextAsync(host, "/soap-ui/app.js");

			// app.js looks every element up by id at startup and refuses to start when one is missing. Read the list from the script itself.
			var table = Regex.Match(script, @"REF_IDS\s*=\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant);
			Assert.IsTrue(table.Success, "app.js must declare REF_IDS.");

			var ids = Regex.Matches(table.Groups["body"].Value, @":\s*'(?<id>[A-Za-z0-9_-]+)'", RegexOptions.CultureInvariant)
				.Cast<Match>()
				.Select(match => match.Groups["id"].Value)
				.ToArray();
			Assert.IsTrue(ids.Length >= 20, "Expected the REF_IDS table to list the page elements, found " + ids.Length + ".");

			foreach (var id in ids)
			{
				StringAssert.Contains(index, "id=\"" + id + "\"", "index.html has no element with id '" + id + "', which app.js requires.");
			}
		}

		[TestMethod]
		[DataRow("app.js", "text/javascript")]
		[DataRow("wsdl-parser.js", "text/javascript")]
		[DataRow("form-editor.js", "text/javascript")]
		[DataRow("xml-view.js", "text/javascript")]
		[DataRow("style.css", "text/css")]
		[DataRow("form-editor.css", "text/css")]
		public async Task Assets_AreServedWithTypeEtagAndSecurityHeaders(string asset, string mediaType)
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.GetRawAsync(host, "/soap-ui/" + asset);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual(mediaType, response.Content.Headers.ContentType.MediaType);
			Assert.AreEqual("utf-8", response.Content.Headers.ContentType.CharSet);
			UiTestHost.AssertSecurityHeaders(response, false);
			Assert.IsNotNull(response.Headers.ETag);
			Assert.IsFalse(string.IsNullOrEmpty(response.Headers.ETag.Tag));
			StringAssert.StartsWith(response.Headers.ETag.Tag, "\"");
			Assert.IsFalse(response.Headers.ETag.IsWeak, "The ETag must be strong.");
			Assert.IsTrue((await response.Content.ReadAsStringAsync()).Length > 0);
		}

		[TestMethod]
		[DataRow("/soap-ui/")]
		[DataRow("/soap-ui/app.js")]
		[DataRow("/soap-ui/wsdl-parser.js")]
		[DataRow("/soap-ui/form-editor.js")]
		[DataRow("/soap-ui/xml-view.js")]
		[DataRow("/soap-ui/style.css")]
		[DataRow("/soap-ui/form-editor.css")]
		public async Task Assets_IfNoneMatch_Returns304KeepingSecurityHeaders(string path)
		{
			using var host = UiTestHost.CreateWithUi(null);
			using var first = await UiTestHost.GetRawAsync(host, path);
			Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
			var etag = first.Headers.ETag;
			Assert.IsNotNull(etag);

			using var second = await UiTestHost.SendAsync(host, HttpMethod.Get, path, request => request.Headers.IfNoneMatch.Add(etag));

			Assert.AreEqual(HttpStatusCode.NotModified, second.StatusCode);
			Assert.AreEqual(0, (await second.Content.ReadAsByteArrayAsync()).Length);
			UiTestHost.AssertSecurityHeaders(second, false);
			Assert.IsNotNull(second.Headers.ETag);
			Assert.AreEqual(etag.Tag, second.Headers.ETag.Tag);
		}

		[TestMethod]
		public async Task Assets_IfNoneMatchStar_Returns304()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.SendAsync(host, HttpMethod.Get, "/soap-ui/app.js", request => request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any));

			Assert.AreEqual(HttpStatusCode.NotModified, response.StatusCode);
			UiTestHost.AssertSecurityHeaders(response, false);
		}

		[TestMethod]
		public async Task Assets_IfNoneMatchWithDifferentEtag_Returns200()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.SendAsync(host, HttpMethod.Get, "/soap-ui/app.js", request => request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"not-the-etag\"")));

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.IsTrue((await response.Content.ReadAsStringAsync()).Length > 0);
		}

		[TestMethod]
		public async Task ApiServices_ReturnsJsonWithSecurityHeadersAndNoStore()
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual("application/json", response.Content.Headers.ContentType.MediaType);
			Assert.AreEqual("utf-8", response.Content.Headers.ContentType.CharSet);
			UiTestHost.AssertSecurityHeaders(response, true);
			Assert.IsTrue(response.Headers.CacheControl.NoCache);
			Assert.IsTrue(response.Headers.CacheControl.NoStore);
			Assert.IsFalse(response.Headers.Contains("Set-Cookie"), "The UI must never set cookies.");
			Assert.IsNull(response.Headers.ETag, "The API response must not carry an ETag.");
		}

		[TestMethod]
		[DataRow("/soap-ui/")]
		[DataRow("/soap-ui/app.js")]
		[DataRow("/soap-ui/form-editor.js")]
		[DataRow("/soap-ui/xml-view.js")]
		[DataRow("/soap-ui/form-editor.css")]
		[DataRow("/soap-ui/api/services")]
		public async Task Head_ReturnsOkWithSecurityHeaders(string path)
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.SendAsync(host, HttpMethod.Head, path);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual(UiTestHost.ExpectedCsp, UiTestHost.GetSingleHeader(response, "Content-Security-Policy"));
		}

		[TestMethod]
		public async Task UnsupportedMethods_Return405WithAllowHeader()
		{
			using var host = UiTestHost.CreateWithUi(null);
			var paths = AssetPaths.Concat(new[] { "/soap-ui/", "/soap-ui/api/services" }).ToArray();
			var methods = new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch };

			foreach (var path in paths)
			{
				foreach (var method in methods)
				{
					using var response = await UiTestHost.SendAsync(host, method, path);

					Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode, method + " " + path);
					CollectionAssert.AreEquivalent(new[] { "GET", "HEAD" }, response.Content.Headers.Allow.ToArray(), method + " " + path);
				}
			}
		}

		[TestMethod]
		[DataRow("/soap-ui/nope.js")]
		[DataRow("/soap-ui/index.html")]
		[DataRow("/soap-ui/..%2f..%2fapp.js")]
		[DataRow("/soap-ui/..%5capp.js")]
		[DataRow("/soap-ui/%2e%2e%2f%2e%2e%2fappsettings.json")]
		[DataRow("/soap-ui//app.js")]
		[DataRow("/soap-ui/api/services/extra")]
		[DataRow("/soap-ui/EmbeddedUI/app.js")]
		[DataRow("/soap-ui/SoapCore.UI.EmbeddedUI.app.js")]
		[DataRow("/soap-ui/..%2fform-editor.js")]
		[DataRow("/soap-ui/..%5cxml-view.js")]
		[DataRow("/soap-ui/%2e%2e%2fform-editor.css")]
		[DataRow("/soap-ui//form-editor.js")]
		[DataRow("/soap-ui//xml-view.js")]
		[DataRow("/soap-ui/EmbeddedUI/form-editor.js")]
		[DataRow("/soap-ui/EmbeddedUI/xml-view.js")]
		[DataRow("/soap-ui/EmbeddedUI/form-editor.css")]
		[DataRow("/soap-ui/SoapCore.UI.EmbeddedUI.form-editor.js")]
		[DataRow("/soap-ui/SoapCore.UI.EmbeddedUI.xml-view.js")]
		[DataRow("/soap-ui/SoapCore.UI.EmbeddedUI.form-editor.css")]
		[DataRow("/soap-ui/form-editor.js.map")]
		[DataRow("/soap-ui/xml-view.min.js")]
		[DataRow("/soap-ui/form-editor.scss")]
		public async Task UnknownPaths_Return404(string rawPath)
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.GetRawAsync(host, rawPath);

			Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, rawPath);
		}

		[TestMethod]
		public async Task InvokeAndProxyRoutes_DoNotExist()
		{
			using var host = UiTestHost.CreateWithUi(null);

			foreach (var path in new[] { "/soap-ui/api/invoke", "/soap-ui/api/options", "/soap-ui/api/proxy", "/soap-ui/api/services/service-svc/wsdl" })
			{
				using var response = await UiTestHost.GetRawAsync(host, path);

				Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, "GET " + path);
			}

			using var post = await UiTestHost.SendAsync(host, HttpMethod.Post, "/soap-ui/api/invoke");
			Assert.AreEqual(HttpStatusCode.NotFound, post.StatusCode, "POST /soap-ui/api/invoke");
		}

		[TestMethod]
		public async Task CustomPattern_ServesUnderRequestedPrefix()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi("/tools/soap"));
				});

			using var api = await UiTestHost.GetRawAsync(host, "/tools/soap/api/services");
			using var oldApi = await UiTestHost.GetRawAsync(host, "/soap-ui/api/services");
			using var redirect = await UiTestHost.GetRawAsync(host, "/tools/soap");

			Assert.AreEqual(HttpStatusCode.OK, api.StatusCode);
			Assert.AreEqual(HttpStatusCode.NotFound, oldApi.StatusCode);
			Assert.AreEqual(HttpStatusCode.PermanentRedirect, redirect.StatusCode);
			Assert.AreEqual("soap/", redirect.Headers.Location.OriginalString);
		}

		[TestMethod]
		public async Task TwoMappings_ServeBothPatterns()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						endpoints.MapSoapCoreUi();
						endpoints.MapSoapCoreUi("/explorer");
					});
				});

			using var first = await UiTestHost.GetJsonAsync(host, "/soap-ui/api/services");
			using var second = await UiTestHost.GetJsonAsync(host, "/explorer/api/services");

			Assert.AreEqual(JsonValueKind.Array, first.RootElement.GetProperty("services").ValueKind);
			Assert.AreEqual(JsonValueKind.Array, second.RootElement.GetProperty("services").ValueKind);
		}

		[TestMethod]
		[DataRow("/soap-ui/")]
		[DataRow("/soap-ui/api/services")]
		public async Task Responses_DoNotReflectRequestInput(string path)
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						endpoints.MapSoapCoreUi();
						endpoints.MapGet("/host", context => context.Response.WriteAsync(context.Request.Host.Value));
					});
				});

			using var plain = await UiTestHost.GetRawAsync(host, path);
			using var hostile = await UiTestHost.GetRawAsync(host, path + "?x=%3Cscript%3Ealert(1)%3C/script%3E");
			var plainBody = await plain.Content.ReadAsByteArrayAsync();
			var hostileBody = await hostile.Content.ReadAsByteArrayAsync();

			Assert.AreEqual(HttpStatusCode.OK, plain.StatusCode);
			Assert.AreEqual(HttpStatusCode.OK, hostile.StatusCode);
			CollectionAssert.AreEqual(plainBody, hostileBody, "The query string must not change the response.");
			var hostileText = await hostile.Content.ReadAsStringAsync();
			Assert.IsFalse(hostileText.Contains("alert(1)", StringComparison.Ordinal));
			if (path.EndsWith("api/services", StringComparison.Ordinal))
			{
				Assert.IsFalse(hostileText.Contains("x=", StringComparison.Ordinal), "The API must not echo the query string.");
			}

			// Proves the test host honours the Host header, so the check below is meaningful.
			using var control = await UiTestHost.SendAsync(host, HttpMethod.Get, "/host", request => request.Headers.Host = "evil.example");
			Assert.AreEqual("evil.example", await control.Content.ReadAsStringAsync());

			using var withHost = await UiTestHost.SendAsync(host, HttpMethod.Get, path, request => request.Headers.Host = "evil.example");
			var withHostBody = await withHost.Content.ReadAsByteArrayAsync();
			CollectionAssert.AreEqual(plainBody, withHostBody, "The Host header must not change the response.");
			Assert.IsFalse((await withHost.Content.ReadAsStringAsync()).Contains("evil.example", StringComparison.Ordinal));
		}

		[TestMethod]
		public async Task UiUnderRoutingModeSoapEndpointPath_Works()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						endpoints.UseSoapEndpoint<TestService>("/soap", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
						endpoints.MapSoapCoreUi("/soap/ui");
					});
				});

			using var index = await UiTestHost.GetRawAsync(host, "/soap/ui/");
			using var api = await UiTestHost.GetRawAsync(host, "/soap/ui/api/services");

			Assert.AreEqual(HttpStatusCode.OK, index.StatusCode);
			Assert.AreEqual(HttpStatusCode.OK, api.StatusCode);
			Assert.AreEqual("text/html", index.Content.Headers.ContentType.MediaType);
			Assert.AreEqual("application/json", api.Content.Headers.ContentType.MediaType);
		}

		private static Match FindScriptTag(string html, string src)
		{
			return Regex.Match(html, @"<script\b[^>]*\bsrc=""" + Regex.Escape(src) + @"""[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
		}
	}
}
