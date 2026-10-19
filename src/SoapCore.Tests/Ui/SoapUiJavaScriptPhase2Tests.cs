using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.Ui
{
	// Unit tests of the Phase 2 additions to the explorer's page independent JavaScript (wsdl-parser.js), run in the Jint
	// interpreter: the code snippet builders, the authorization headers, the WS-Security UsernameToken, UTF-8, Base64 and SHA-1,
	// and the sample values for CLR types. applyWsSecurity and the parameters of fallbackEnvelope need a DOM, so only their
	// failure paths run here. Test data written as JavaScript source (parameters named ...Source) keeps control characters
	// and unpaired surrogates out of the test names and out of the JSON encoder. The script still receives the real characters.
	[TestClass]
	public class SoapUiJavaScriptPhase2Tests
	{
		private const string Url = "https://example.com/Service.svc";

		private const string Action = "http://tempuri.org/ISampleService/Ping";

		private const string Soap11Type = "text/xml; charset=utf-8";

		private const string Soap12Type = "application/soap+xml; charset=utf-8; action=\"http://tempuri.org/ISampleService/Ping\"";

		private const string Phase2Functions = "['buildSnippet', 'buildAuthHeader', 'wsPasswordDigest', 'utf8Bytes', 'base64Encode', 'buildWsSecurityHeader', 'applyWsSecurity', 'sampleForClrType']";

		private const string WsseNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

		private const string WsuNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

		private const string PasswordTextType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

		private const string PasswordDigestType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest";

		private const string Base64BinaryType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";

		private const string Created = "2025-01-31T12:00:00Z";

		// Bytes 0 to 15, as JavaScript source and as the Base64 of them.
		private const string NonceSource = "[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]";

		private const string NonceBase64 = "AAECAwQFBgcICQoLDA0ODw==";

		private static readonly string[] AllKinds = { "curl", "powershell", "csharp", "http" };

		private static readonly string Soap11Body = Join(
			"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\">",
			"\t<soapenv:Body>",
			"\t\t<Ping xmlns=\"http://tempuri.org/\"/>",
			"\t</soapenv:Body>",
			"</soapenv:Envelope>");

		private static readonly string Soap12Body = Join(
			"<soapenv:Envelope xmlns:soapenv=\"http://www.w3.org/2003/05/soap-envelope\">",
			"\t<soapenv:Body>",
			"\t\t<Ping xmlns=\"http://tempuri.org/\"/>",
			"\t</soapenv:Body>",
			"</soapenv:Envelope>");

		// ------------------------------------------------------------------
		// Surface
		// ------------------------------------------------------------------
		[TestMethod]
		public void Surface_ExposesTheFunctionsOfPhase2()
		{
			var expression = "(function (names) { return names.filter(function (name) { return typeof window.SoapUiWsdl[name] !== 'function'; }); }(" + Phase2Functions + "))";

			var missing = Strings(SoapUiJavaScriptHost.Evaluate(expression));

			Assert.AreEqual(0, missing.Length, "Missing functions on window.SoapUiWsdl: " + string.Join(", ", missing));
		}

		[TestMethod]
		public void Surface_SnippetKindsIsAFrozenArrayOfFrozenEntries()
		{
			var expression = "(function (kinds) { return [Array.isArray(kinds), Object.isFrozen(kinds), kinds.every(function (kind) { return Object.isFrozen(kind); })]; }(window.SoapUiWsdl.snippetKinds))";

			var flags = SoapUiJavaScriptHost.Evaluate(expression).EnumerateArray().Select(item => item.GetBoolean()).ToArray();

			CollectionAssert.AreEqual(new[] { true, true, true }, flags);
		}

		[TestMethod]
		public void Surface_Phase2ExportsCannotBeReplaced()
		{
			var expression = "(function (api) { try { api.buildSnippet = null; api.snippetKinds = null; api.sampleForClrType = null; } catch (error) { } return [typeof api.buildSnippet, typeof api.snippetKinds, typeof api.sampleForClrType]; }(window.SoapUiWsdl))";

			var kinds = Strings(SoapUiJavaScriptHost.Evaluate(expression));

			CollectionAssert.AreEqual(new[] { "function", "object", "function" }, kinds);
		}

		// ------------------------------------------------------------------
		// snippetKinds
		// ------------------------------------------------------------------
		[TestMethod]
		public void SnippetKinds_ListsTheFourKindsWithTheirLabelsInOrder()
		{
			var kinds = SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.snippetKinds")
				.EnumerateArray()
				.Select(kind => kind.GetProperty("id").GetString() + "|" + kind.GetProperty("label").GetString())
				.ToArray();

			CollectionAssert.AreEqual(new[] { "curl|curl (bash)", "powershell|PowerShell", "csharp|C# (HttpClient)", "http|Raw HTTP" }, kinds);
		}

		[TestMethod]
		public void SnippetKinds_EveryListedKindBuildsASnippet()
		{
			var ids = SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.snippetKinds.map(function (kind) { return kind.id; })");

			Assert.AreEqual(AllKinds.Length, ids.GetArrayLength());
			foreach (var id in ids.EnumerateArray().Select(item => item.GetString()))
			{
				AssertOk(BuildSnippet(id, Url, "<a/>", "Content-Type", Soap11Type));
			}
		}

		// ------------------------------------------------------------------
		// buildSnippet: all kinds
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildSnippet_Curl_IsIdenticalToCurlBuild()
		{
			var requests = new[]
			{
				RequestSource(Url, Soap11Body, "Content-Type", Soap11Type, "SOAPAction", "\"" + Action + "\""),
				RequestSource(Url, Soap12Body, "Content-Type", Soap12Type, "Authorization", "Bearer secret"),
				RequestSource("https://h/s?a=b'c", "x\n\nSOAP_EOF\ny"),
				RequestSource(Url, string.Empty),
			};

			foreach (var request in requests)
			{
				var snippet = Call("buildSnippet", "'curl'", request);
				var curl = Call("curl.build", request);

				AssertOk(snippet);
				Assert.AreEqual(curl.GetProperty("text").GetString(), snippet.GetProperty("text").GetString());
			}
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("''")]
		[DataRow("'nope'")]
		[DataRow("'CURL'")]
		[DataRow("'PowerShell'")]
		[DataRow("'cs'")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("['curl']")]
		[DataRow("'__proto__'")]
		[DataRow("'toString'")]
		[DataRow("'constructor'")]
		public void BuildSnippet_UnknownKind_Fails(string kindSource)
		{
			var result = Call("buildSnippet", kindSource, RequestSource(Url, "<a/>"));

			AssertFailed(result);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("'nope'")]
		[DataRow("5")]
		[DataRow("true")]
		[DataRow("[]")]
		[DataRow("[{ url: 'https://h/x' }]")]
		public void BuildSnippet_RequestThatIsNotAnObject_FailsForEveryKind(string requestSource)
		{
			foreach (var kind in AllKinds)
			{
				AssertFailed(Call("buildSnippet", Lit(kind), requestSource));
			}
		}

		[TestMethod]
		[DataRow("{}")]
		[DataRow("{ headers: [], body: 'x' }")]
		[DataRow("{ url: '', headers: [], body: 'x' }")]
		[DataRow("{ url: null, headers: [], body: 'x' }")]
		[DataRow("{ url: undefined, headers: [], body: 'x' }")]
		public void BuildSnippet_WithoutAUrl_FailsForEveryKind(string requestSource)
		{
			foreach (var kind in AllKinds)
			{
				AssertFailed(Call("buildSnippet", Lit(kind), requestSource));
			}
		}

		[TestMethod]
		[DataRow("'https://h/\\n'")]
		[DataRow("'https://h/\\r\\nHost: evil'")]
		[DataRow("'https://h/a\\tb'")]
		[DataRow("'https://h/\\u0000'")]
		[DataRow("'https://h/\\u001b[31m'")]
		[DataRow("'https://h/\\u007f'")]
		public void BuildSnippet_RejectsAUrlWithControlCharacters_ForEveryKind(string urlSource)
		{
			foreach (var kind in AllKinds)
			{
				AssertFailed(Call("buildSnippet", Lit(kind), "{ url: " + urlSource + ", headers: [], body: 'x' }"));
			}
		}

		[TestMethod]
		[DataRow("'X-A\\n'", "'v'")]
		[DataRow("'X\\rA'", "'v'")]
		[DataRow("'X-A\\u0000'", "'v'")]
		[DataRow("'X-A\\tB'", "'v'")]
		[DataRow("''", "'v'")]
		[DataRow("'X-A'", "'a\\nb'")]
		[DataRow("'X-A'", "'a\\rb'")]
		[DataRow("'X-A'", "'a\\r\\nInjected: 1'")]
		[DataRow("'X-A'", "'a\\u0000b'")]
		[DataRow("'X-A'", "'a\\u001bb'")]
		[DataRow("'X-A'", "'a\\u007fb'")]
		[DataRow("'Authorization'", "'a\\nb'")]
		[DataRow("'Cookie'", "'x\\r\\nSet-Cookie: y'")]
		public void BuildSnippet_RejectsHeadersWithAnEmptyNameOrControlCharacters_ForEveryKind(string nameSource, string valueSource)
		{
			foreach (var kind in AllKinds)
			{
				var result = Call("buildSnippet", Lit(kind), "{ url: " + Lit(Url) + ", headers: [{ name: " + nameSource + ", value: " + valueSource + " }], body: 'x' }");

				AssertFailed(result);
			}
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("{ value: 'x' }")]
		[DataRow("{ name: '', value: 'x' }")]
		[DataRow("{ name: undefined }")]
		[DataRow("{ name: null, value: 'x' }")]
		public void BuildSnippet_RejectsAHeaderWithoutAName_ForEveryKind(string headerSource)
		{
			foreach (var kind in AllKinds)
			{
				var result = Call("buildSnippet", Lit(kind), "{ url: " + Lit(Url) + ", headers: [" + headerSource + "], body: 'x' }");

				AssertFailed(result);
			}
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("'nope'")]
		[DataRow("{}")]
		[DataRow("5")]
		public void BuildSnippet_IgnoresAHeaderListThatIsNotAnArray_ForEveryKind(string headersSource)
		{
			foreach (var kind in AllKinds)
			{
				var result = Call("buildSnippet", Lit(kind), "{ url: " + Lit(Url) + ", headers: " + headersSource + ", body: 'x' }");

				AssertOk(result);
			}
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("''")]
		public void BuildSnippet_AMissingHeaderValueIsAnEmptyValue_ForEveryKind(string valueSource)
		{
			foreach (var kind in AllKinds)
			{
				var result = Call("buildSnippet", Lit(kind), "{ url: " + Lit(Url) + ", headers: [{ name: 'X-A', value: " + valueSource + " }], body: 'x' }");

				AssertOk(result);
				StringAssert.Contains(result.GetProperty("text").GetString(), HeaderLine(kind, "X-A", string.Empty), kind);
			}
		}

		[TestMethod]
		public void BuildSnippet_AcceptsATabInAHeaderValue_ForEveryKind()
		{
			var expectedPart = new Dictionary<string, string>
			{
				{ "curl", "-H 'X-Note: a\tb'" },
				{ "powershell", "'X-Note' = 'a\tb'" },
				{ "csharp", "TryAddWithoutValidation(\"X-Note\", \"a\\tb\")" },
				{ "http", "\nX-Note: a\tb\n" },
			};

			foreach (var kind in AllKinds)
			{
				var result = BuildSnippet(kind, Url, "<a/>", "X-Note", "a\tb");

				AssertOk(result);
				StringAssert.Contains(result.GetProperty("text").GetString(), expectedPart[kind], kind);
			}
		}

		[TestMethod]
		[DataRow("'a\\u0000b'")]
		[DataRow("'a\\u0001b'")]
		[DataRow("'a\\u0008b'")]
		[DataRow("'a\\u000bb'")]
		[DataRow("'a\\u000cb'")]
		[DataRow("'a\\u001bb'")]
		[DataRow("'a\\u007fb'")]
		public void BuildSnippet_RejectsOtherControlCharactersInTheBody_ForEveryKind(string bodySource)
		{
			foreach (var kind in AllKinds)
			{
				AssertFailed(Call("buildSnippet", Lit(kind), "{ url: " + Lit(Url) + ", headers: [], body: " + bodySource + " }"));
			}
		}

		[TestMethod]
		public void BuildSnippet_AcceptsTabCarriageReturnAndLineFeedInTheBody_ForEveryKind()
		{
			foreach (var kind in AllKinds)
			{
				var result = Call("buildSnippet", Lit(kind), "{ url: " + Lit(Url) + ", headers: [], body: 'a\\tb\\r\\nc' }");

				AssertOk(result);
				StringAssert.Contains(result.GetProperty("text").GetString(), "a\tb\nc", kind);
			}
		}

		[TestMethod]
		public void BuildSnippet_NormalisesLineBreaksOfTheBodyToLf_ForEveryKind()
		{
			foreach (var kind in AllKinds)
			{
				var result = BuildSnippet(kind, Url, "a\r\nb\rc\nd");

				AssertOk(result);
				var text = result.GetProperty("text").GetString();
				Assert.IsFalse(text.Contains('\r'), kind + ": the snippet must not contain a carriage return.");
				StringAssert.Contains(text, "a\nb\nc\nd", kind);
			}
		}

		[TestMethod]
		public void BuildSnippet_RedactsTheValuesOfSensitiveHeaders_ForEveryKind()
		{
			var headers = new[]
			{
				"Content-Type", Soap11Type,
				"Authorization", "Bearer top-secret-token",
				"Cookie", "session=cookie-secret",
				"X-Api-Key", "api-key-secret",
				"X-Session-Id", "session-id-secret",
				"X-CSRF-Token", "csrf-secret",
				"X-Trace-Id", "trace-visible"
			};
			var secrets = new[] { "top-secret-token", "cookie-secret", "api-key-secret", "session-id-secret", "csrf-secret" };

			foreach (var kind in AllKinds)
			{
				var result = BuildSnippet(kind, Url, "<a/>", headers);

				AssertOk(result);
				var text = result.GetProperty("text").GetString();
				foreach (var name in new[] { "Authorization", "Cookie", "X-Api-Key", "X-Session-Id", "X-CSRF-Token" })
				{
					StringAssert.Contains(text, HeaderLine(kind, name, "REDACTED"), kind);
				}

				StringAssert.Contains(text, HeaderLine(kind, "X-Trace-Id", "trace-visible"), kind);
				foreach (var secret in secrets)
				{
					Assert.IsFalse(text.Contains(secret), kind + ": the secret " + secret + " leaked into the snippet.");
				}
			}
		}

		[TestMethod]
		public void BuildSnippet_DoesNotRedactTheBody_ForEveryKind()
		{
			foreach (var kind in AllKinds)
			{
				var result = BuildSnippet(kind, Url, "<password>hunter2</password>");

				AssertOk(result);
				StringAssert.Contains(result.GetProperty("text").GetString(), "<password>hunter2</password>", kind);
			}
		}

		// ------------------------------------------------------------------
		// buildSnippet: PowerShell
		// ------------------------------------------------------------------
		[TestMethod]
		public void Powershell_BuildsAScriptForASoap11Request()
		{
			var result = BuildSnippet("powershell", Url, Soap11Body, "Content-Type", Soap11Type, "SOAPAction", "\"" + Action + "\"");

			AssertOk(result);
			var expected = Lines(
				"$headers = @{",
				"    'SOAPAction' = '\"http://tempuri.org/ISampleService/Ping\"'",
				"}",
				"$body = @'",
				Soap11Body,
				"'@",
				"$response = Invoke-WebRequest -Uri 'https://example.com/Service.svc' -Method Post -Headers $headers -ContentType 'text/xml; charset=utf-8' -Body $body",
				"$response.Content");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Powershell_BuildsAScriptForASoap12Request()
		{
			var result = BuildSnippet("powershell", Url, Soap12Body, "Content-Type", Soap12Type);

			AssertOk(result);
			var expected = Lines(
				"$headers = @{}",
				"$body = @'",
				Soap12Body,
				"'@",
				"$response = Invoke-WebRequest -Uri 'https://example.com/Service.svc' -Method Post -Headers $headers -ContentType 'application/soap+xml; charset=utf-8; action=\"http://tempuri.org/ISampleService/Ping\"' -Body $body",
				"$response.Content");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Powershell_KeepsTheOrderOfTheHeadersAndLeavesContentTypeOutOfTheHashtable()
		{
			var result = BuildSnippet("powershell", Url, "<a/>", "X-One", "1", "content-type", "text/xml", "X-Two", "2", "Content-Type", "text/plain");

			AssertOk(result);
			var expected = Lines(
				"$headers = @{",
				"    'X-One' = '1'",
				"    'X-Two' = '2'",
				"}",
				"$body = @'",
				"<a/>",
				"'@",
				"$response = Invoke-WebRequest -Uri 'https://example.com/Service.svc' -Method Post -Headers $headers -ContentType 'text/xml' -Body $body",
				"$response.Content");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Powershell_WithoutAContentType_OmitsTheContentTypeParameter()
		{
			var result = BuildSnippet("powershell", Url, "<a/>", "X-One", "1");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			Assert.IsFalse(text.Contains("-ContentType"), "There is no Content-Type to pass: " + text);
			StringAssert.Contains(text, "-Headers $headers -Body $body\n");
		}

		[TestMethod]
		public void Powershell_DoublesSingleQuotesInTheUrlHeadersAndContentType()
		{
			var result = BuildSnippet("powershell", "https://h/s?a=b'c", "x", "X'A", "it's", "Content-Type", "text/xml; note='x'");

			AssertOk(result);
			var expected = Lines(
				"$headers = @{",
				"    'X''A' = 'it''s'",
				"}",
				"$body = @'",
				"x",
				"'@",
				"$response = Invoke-WebRequest -Uri 'https://h/s?a=b''c' -Method Post -Headers $headers -ContentType 'text/xml; note=''x''' -Body $body",
				"$response.Content");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Powershell_DoublesTheTypographicSingleQuotesToo()
		{
			// PowerShell reads U+2018 to U+201B as single quotes, so they would end the string like an apostrophe.
			var quotes = new[] { char.ConvertFromUtf32(0x2018), char.ConvertFromUtf32(0x2019), char.ConvertFromUtf32(0x201A), char.ConvertFromUtf32(0x201B) };

			foreach (var quote in quotes)
			{
				var result = BuildSnippet("powershell", "https://h/a" + quote + "b", "x", "X-A", "it" + quote + "s");

				AssertOk(result);
				var text = result.GetProperty("text").GetString();
				StringAssert.Contains(text, "-Uri 'https://h/a" + quote + quote + "b'");
				StringAssert.Contains(text, "'X-A' = 'it" + quote + quote + "s'");
			}
		}

		[TestMethod]
		public void Powershell_ShellMetacharactersInHeadersAndTheUrlStayLiteral()
		{
			var result = BuildSnippet("powershell", "https://h/$(id)?a=1&b=`id`", "x", "X-A", "$(id) `id` \"q\" ${X} $HOME \\ ; & |");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			StringAssert.Contains(text, "    'X-A' = '$(id) `id` \"q\" ${X} $HOME \\ ; & |'\n");
			StringAssert.Contains(text, "-Uri 'https://h/$(id)?a=1&b=`id`' ");
		}

		[TestMethod]
		public void Powershell_MetacharactersInTheBodyStayLiteralInTheHereString()
		{
			var body = "$(id) `id` \"q\" 's' \\ ${X} $HOME";

			var result = BuildSnippet("powershell", Url, body);

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "$body = @'\n" + body + "\n'@\n");
		}

		[TestMethod]
		public void Powershell_EmptyBody_StillBuildsAWellFormedHereString()
		{
			var result = BuildSnippet("powershell", Url, string.Empty);

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "$body = @'\n\n'@\n");
		}

		[TestMethod]
		public void Powershell_LeadingAndTrailingLineBreaksOfTheBody_ArePreserved()
		{
			var result = BuildSnippet("powershell", Url, "\nx\n");

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "$body = @'\n\nx\n\n'@\n");
		}

		[TestMethod]
		public void Powershell_NonAsciiTextIsKeptAsItIs()
		{
			var body = "<a>caf\u00e9 \u65e5\u672c \U0001F600</a>";

			var result = BuildSnippet("powershell", Url, body, "X-Name", "Jos\u00e9");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			StringAssert.Contains(text, "$body = @'\n" + body + "\n'@\n");
			StringAssert.Contains(text, "'X-Name' = 'Jos\u00e9'");
		}

		[TestMethod]
		[DataRow("'\\'@'")]
		[DataRow("'x\\n\\'@'")]
		[DataRow("'\\'@ trailing'")]
		[DataRow("'a\\n\\'@\\nb'")]
		[DataRow("'a\\r\\n\\'@'")]
		[DataRow("'a\\r\\'@'")]
		[DataRow("'\\u2018@'")]
		[DataRow("'\\u2019@'")]
		[DataRow("'\\u201a@'")]
		[DataRow("'\\u201b@'")]
		public void Powershell_BodyWithALineThatEndsAHereString_Fails(string bodySource)
		{
			var result = Call("buildSnippet", "'powershell'", "{ url: " + Lit(Url) + ", headers: [], body: " + bodySource + " }");

			AssertFailed(result);
			StringAssert.Contains(result.GetProperty("error").GetString(), "here-string");
		}

		[TestMethod]
		[DataRow("' \\'@'")]
		[DataRow("'x\\'@'")]
		[DataRow("'\\'x@'")]
		[DataRow("'@\\''")]
		[DataRow("'\\'\\n@'")]
		[DataRow("'\"@'")]
		[DataRow("'\\u2019 @'")]
		[DataRow("'a\\n b\\n  \\'@'")]
		public void Powershell_BodyWithTheTerminatorCharactersElsewhere_IsAccepted(string bodySource)
		{
			var result = Call("buildSnippet", "'powershell'", "{ url: " + Lit(Url) + ", headers: [], body: " + bodySource + " }");

			AssertOk(result);
		}

		[TestMethod]
		public void Powershell_TheTerminatorCheckDoesNotApplyToOtherKinds()
		{
			foreach (var kind in new[] { "curl", "csharp", "http" })
			{
				AssertOk(BuildSnippet(kind, Url, "a\n'@\nb"));
			}
		}

		// ------------------------------------------------------------------
		// buildSnippet: C#
		// ------------------------------------------------------------------
		[TestMethod]
		public void Csharp_BuildsAProgramForASoap11Request()
		{
			var result = BuildSnippet("csharp", Url, Soap11Body, "Content-Type", Soap11Type, "SOAPAction", "\"" + Action + "\"");

			AssertOk(result);
			var expected = Lines(
				"using System;",
				"using System.Net.Http;",
				"using System.Text;",
				string.Empty,
				"using var client = new HttpClient();",
				"using var request = new HttpRequestMessage(HttpMethod.Post, \"https://example.com/Service.svc\");",
				"request.Headers.TryAddWithoutValidation(\"SOAPAction\", \"\\\"http://tempuri.org/ISampleService/Ping\\\"\");",
				"var body = \"\"\"",
				Soap11Body,
				"\"\"\";",
				"request.Content = new StringContent(body, Encoding.UTF8, \"text/xml\");",
				"using var response = await client.SendAsync(request);",
				"Console.WriteLine(await response.Content.ReadAsStringAsync());");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Csharp_BuildsAProgramForASoap12Request()
		{
			var result = BuildSnippet("csharp", Url, Soap12Body, "Content-Type", Soap12Type);

			AssertOk(result);
			var expected = Lines(
				"using System;",
				"using System.Net.Http;",
				"using System.Text;",
				string.Empty,
				"using var client = new HttpClient();",
				"using var request = new HttpRequestMessage(HttpMethod.Post, \"https://example.com/Service.svc\");",
				"var body = \"\"\"",
				Soap12Body,
				"\"\"\";",
				"request.Content = new StringContent(body, Encoding.UTF8, \"application/soap+xml\");",
				"using var response = await client.SendAsync(request);",
				"Console.WriteLine(await response.Content.ReadAsStringAsync());");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Csharp_ContentTypeIsNeverAddedAsARequestHeader()
		{
			var result = BuildSnippet("csharp", Url, "<a/>", "Content-Type", "text/xml", "content-type", "text/plain", "X-One", "1");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			Assert.IsFalse(text.Contains("\"Content-Type\""), "Content-Type must not be a request header: " + text);
			Assert.IsFalse(text.Contains("\"content-type\""), "Content-Type must not be a request header: " + text);
			StringAssert.Contains(text, "request.Headers.TryAddWithoutValidation(\"X-One\", \"1\");\n");
			StringAssert.Contains(text, "new StringContent(body, Encoding.UTF8, \"text/xml\")");
		}

		[TestMethod]
		[DataRow("text/xml; charset=utf-8", "\"text/xml\"")]
		[DataRow("text/xml;charset=utf-8", "\"text/xml\"")]
		[DataRow("text/xml", "\"text/xml\"")]
		[DataRow("  text/xml  ; charset=utf-8", "\"text/xml\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"urn:a\"", "\"application/soap+xml\"")]
		[DataRow("application/soap+xml;action=\"urn:a\"", "\"application/soap+xml\"")]
		[DataRow(";charset=utf-8", null)]
		[DataRow("", null)]
		public void Csharp_TakesTheMediaTypeFromTheContentTypeHeader(string contentType, string expectedLiteral)
		{
			var result = BuildSnippet("csharp", Url, "<a/>", "Content-Type", contentType);

			AssertOk(result);
			var expectedCall = expectedLiteral == null ? "new StringContent(body, Encoding.UTF8);\n" : "new StringContent(body, Encoding.UTF8, " + expectedLiteral + ");\n";
			StringAssert.Contains(result.GetProperty("text").GetString(), expectedCall);
		}

		[TestMethod]
		public void Csharp_WithoutAContentType_UsesAStringContentWithoutAMediaType()
		{
			var result = BuildSnippet("csharp", Url, "<a/>", "X-One", "1");

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "request.Content = new StringContent(body, Encoding.UTF8);\n");
		}

		[TestMethod]
		[DataRow("'plain'", "\"plain\"")]
		[DataRow("'a\"b'", "\"a\\\"b\"")]
		[DataRow("'a\\\\b'", "\"a\\\\b\"")]
		[DataRow("'\\\\\"'", "\"\\\\\\\"\"")]
		[DataRow("'a\\tb'", "\"a\\tb\"")]
		[DataRow("'a\\u0085b'", "\"a\\u0085b\"")]
		[DataRow("'a\\u2028b'", "\"a\\u2028b\"")]
		[DataRow("'a\\u2029b'", "\"a\\u2029b\"")]
		[DataRow("'a\\ud800b'", "\"a\\uD800b\"")]
		[DataRow("'a\\udc00b'", "\"a\\uDC00b\"")]
		[DataRow("'\\ud800a'", "\"\\uD800a\"")]
		[DataRow("'$(id) `x` {0} \\'q\\' ;'", "\"$(id) `x` {0} 'q' ;\"")]
		public void Csharp_EscapesTheStringLiteralsOfHeaderValues(string valueSource, string expectedLiteral)
		{
			var result = Call("buildSnippet", "'csharp'", "{ url: " + Lit(Url) + ", headers: [{ name: 'X-A', value: " + valueSource + " }], body: 'x' }");

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "request.Headers.TryAddWithoutValidation(\"X-A\", " + expectedLiteral + ");\n");
		}

		[TestMethod]
		public void Csharp_EscapesTheUrlAndHeaderNames()
		{
			var result = BuildSnippet("csharp", "https://h/s?a=\"b\"\\c", "x", "X\"A", "v");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			StringAssert.Contains(text, "new HttpRequestMessage(HttpMethod.Post, \"https://h/s?a=\\\"b\\\"\\\\c\");\n");
			StringAssert.Contains(text, "TryAddWithoutValidation(\"X\\\"A\", \"v\");\n");
		}

		[TestMethod]
		public void Csharp_KeepsNonAsciiCharactersAndSurrogatePairsInLiterals()
		{
			var value = "Jos\u00e9 \u65e5\u672c \U0001F600";

			var result = BuildSnippet("csharp", Url, "<a>" + value + "</a>", "X-Name", value);

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			StringAssert.Contains(text, "TryAddWithoutValidation(\"X-Name\", \"" + value + "\");\n");
			StringAssert.Contains(text, "\n<a>" + value + "</a>\n");
		}

		[TestMethod]
		[DataRow("'<a/>'", 3)]
		[DataRow("''", 3)]
		[DataRow("'a\"b'", 3)]
		[DataRow("'\"'.repeat(2)", 3)]
		[DataRow("'\"'.repeat(3)", 4)]
		[DataRow("'\"'.repeat(4)", 5)]
		[DataRow("'\"'.repeat(5)", 6)]
		[DataRow("'\"'.repeat(12)", 13)]
		[DataRow("'\"'.repeat(2) + ' ' + '\"'.repeat(4) + ' ' + '\"'", 5)]
		[DataRow("'say ' + '\"'.repeat(3) + 'hi' + '\"'.repeat(3)", 4)]
		[DataRow("'\"'.repeat(3) + '\\n' + '\"'.repeat(6) + '\\n' + '\"'", 7)]
		[DataRow("'\"'.repeat(7)", 8)]
		public void Csharp_RawStringDelimiter_IsLongerThanTheLongestRunOfDoubleQuotes(string bodySource, int expectedQuotes)
		{
			var result = Call("buildSnippet", "'csharp'", "{ url: " + Lit(Url) + ", headers: [], body: " + bodySource + " }");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			var delimiter = new string('"', expectedQuotes);
			StringAssert.Contains(text, "var body = " + delimiter + "\n", "The opening delimiter must be " + expectedQuotes + " quotes: " + text);
			StringAssert.Contains(text, "\n" + delimiter + ";\n", "The closing delimiter must be " + expectedQuotes + " quotes, on its own line: " + text);
		}

		[TestMethod]
		public void Csharp_RawString_UsesTheBodyLinesUnindented_WithTheClosingDelimiterAtColumnZero()
		{
			var body = Join("<a>", "  <b>say \"\"\"hi\"\"\"</b>", "\t<c/>", "</a>");

			var result = BuildSnippet("csharp", Url, body);

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			StringAssert.Contains(text, "var body = \"\"\"\"\n" + body + "\n\"\"\"\";\n");
			Assert.IsTrue(text.Split('\n').Contains("\"\"\"\";"), "The closing delimiter must start its line: " + text);
		}

		[TestMethod]
		public void Csharp_EmptyBody_StillBuildsAWellFormedRawString()
		{
			var result = BuildSnippet("csharp", Url, string.Empty);

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "var body = \"\"\"\n\n\"\"\";\n");
		}

		[TestMethod]
		[DataRow("\n", "var body = \"\"\"\n\n\n\"\"\";\n")]
		[DataRow("\nx\n", "var body = \"\"\"\n\nx\n\n\"\"\";\n")]
		[DataRow("x\n\n", "var body = \"\"\"\nx\n\n\n\"\"\";\n")]
		[DataRow("\n\n", "var body = \"\"\"\n\n\n\n\"\"\";\n")]
		public void Csharp_LeadingAndTrailingLineBreaksOfTheBody_ArePreserved(string body, string expectedPart)
		{
			var result = BuildSnippet("csharp", Url, body);

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), expectedPart);
		}

		[TestMethod]
		public void Csharp_MetacharactersInTheBodyStayLiteral()
		{
			var body = "$(id) `id` {0} {{x}} \\n \\\\ 's' \\u0041 $\"";

			var result = BuildSnippet("csharp", Url, body);

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "var body = \"\"\"\n" + body + "\n\"\"\";\n");
		}

		// ------------------------------------------------------------------
		// buildSnippet: raw HTTP
		// ------------------------------------------------------------------
		[TestMethod]
		public void Http_BuildsAMessageForASoap11Request()
		{
			var result = BuildSnippet("http", Url, Soap11Body, "Content-Type", Soap11Type, "SOAPAction", "\"" + Action + "\"");

			AssertOk(result);
			var expected = Join(
				"POST /Service.svc HTTP/1.1",
				"Host: example.com",
				"Content-Type: text/xml; charset=utf-8",
				"SOAPAction: \"http://tempuri.org/ISampleService/Ping\"",
				"Content-Length: " + Encoding.UTF8.GetByteCount(Soap11Body),
				string.Empty,
				Soap11Body);
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Http_BuildsAMessageForASoap12Request()
		{
			var result = BuildSnippet("http", Url, Soap12Body, "Content-Type", Soap12Type);

			AssertOk(result);
			var expected = Join(
				"POST /Service.svc HTTP/1.1",
				"Host: example.com",
				"Content-Type: application/soap+xml; charset=utf-8; action=\"http://tempuri.org/ISampleService/Ping\"",
				"Content-Length: " + Encoding.UTF8.GetByteCount(Soap12Body),
				string.Empty,
				Soap12Body);
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void Http_TheTextHasNoFinalLineBreak_SoContentLengthIsTheLengthOfTheBodyShown()
		{
			var result = BuildSnippet("http", Url, "<a/>", "Content-Type", Soap11Type);

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			Assert.IsTrue(text.EndsWith("\nContent-Length: 4\n\n<a/>", StringComparison.Ordinal), text);
		}

		[TestMethod]
		[DataRow("''", 0)]
		[DataRow("'abc'", 3)]
		[DataRow("'caf\\u00e9'", 5)]
		[DataRow("'\\u20ac'", 3)]
		[DataRow("'\\u65e5\\u672c'", 6)]
		[DataRow("'\\ud83d\\ude00'", 4)]
		[DataRow("'a\\ud83d\\ude00b'", 6)]
		[DataRow("'\\ud800'", 3)]
		[DataRow("'\\ude00'", 3)]
		[DataRow("'a\\ud800b'", 5)]
		[DataRow("'a\\r\\nb'", 3)]
		[DataRow("'a\\nb\\n'", 4)]
		[DataRow("'\\t'", 1)]
		public void Http_ContentLength_IsTheUtf8ByteLengthOfTheBody(string bodySource, int expectedBytes)
		{
			// The Content-Length is read in the script, because the text of a body with an unpaired surrogate cannot be read as JSON text here.
			var expression = "(function (request) { var result = window.SoapUiWsdl.buildSnippet('http', request); return result.ok ? /\\nContent-Length: (\\d+)\\n\\n/.exec(result.text)[1] : result.error; }({ url: " + Lit(Url) + ", headers: [], body: " + bodySource + " }))";

			var length = SoapUiJavaScriptHost.Evaluate(expression).GetString();

			Assert.AreEqual(expectedBytes.ToString(CultureInfo.InvariantCulture), length);
		}

		[TestMethod]
		public void Http_ContentLength_MatchesTheDotNetEncodingForAssortedTexts()
		{
			var texts = new[] { string.Empty, "<a/>", "caf\u00e9", "\u65e5\u672c\u8a9e", "\U0001F600\U0001F680", "mixed \u00e9 \u20ac \U0001F600 text", new string('x', 5000), string.Concat(Enumerable.Repeat("\u00e9\u20ac\U0001F600", 700)) };

			foreach (var body in texts)
			{
				var result = BuildSnippet("http", Url, body);

				AssertOk(result);
				StringAssert.Contains(result.GetProperty("text").GetString(), "\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\n\n");
			}
		}

		[TestMethod]
		[DataRow("https://example.com/Service.svc", "/Service.svc", "example.com")]
		[DataRow("https://example.com", "/", "example.com")]
		[DataRow("https://example.com/", "/", "example.com")]
		[DataRow("https://example.com?x=1", "/?x=1", "example.com")]
		[DataRow("http://localhost:5000/soap/Svc.svc?wsdl&a=b", "/soap/Svc.svc?wsdl&a=b", "localhost:5000")]
		[DataRow("https://user:pw@example.com:8443/a#frag", "/a", "example.com:8443")]
		[DataRow("https://user@example.com/a", "/a", "example.com")]
		[DataRow("https://@example.com/a", "/a", "example.com")]
		[DataRow("https://[::1]:5000/x", "/x", "[::1]:5000")]
		[DataRow("HTTP://Example.com/x", "/x", "Example.com")]
		[DataRow("https://h/a b", "/a%20b", "h")]
		[DataRow("https://h/?q=a b", "/?q=a%20b", "h")]
		[DataRow("https://h/a%20b", "/a%20b", "h")]
		[DataRow("https://h/caf\u00e9", "/caf%C3%A9", "h")]
		[DataRow("https://h/\U0001F600", "/%F0%9F%98%80", "h")]
		[DataRow("https://h/a#", "/a", "h")]
		public void Http_TakesTheHostAndTheRequestTargetFromTheUrl(string url, string target, string host)
		{
			var result = BuildSnippet("http", url, "x");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			Assert.IsTrue(text.StartsWith("POST " + target + " HTTP/1.1\nHost: " + host + "\n", StringComparison.Ordinal), "Unexpected start of the message: " + text);
		}

		[TestMethod]
		[DataRow("/relative")]
		[DataRow("//h/x")]
		[DataRow("h/x")]
		[DataRow("ftp://h/x")]
		[DataRow("file:///x")]
		[DataRow("mailto:x@h")]
		[DataRow("httpx://h/x")]
		[DataRow("http:/h")]
		[DataRow("https:///x")]
		[DataRow("https://user@/x")]
		[DataRow("https://h x/y")]
		[DataRow("https://h\u00e9/x")]
		public void Http_RequiresAnAbsoluteHttpOrHttpsUrlWithAnAsciiHost(string url)
		{
			var result = BuildSnippet("http", url, "x");

			AssertFailed(result);
		}

		[TestMethod]
		public void Http_RelativeUrlsStillWorkForTheOtherKinds()
		{
			foreach (var kind in new[] { "curl", "powershell", "csharp" })
			{
				AssertOk(BuildSnippet(kind, "/soap/Service.svc", "x"));
			}
		}

		[TestMethod]
		public void Http_ComputesHostAndContentLength_SoHeadersWithThoseNamesAreLeftOut()
		{
			var result = BuildSnippet("http", Url, "<a/>", "Host", "evil.example", "X-One", "1", "content-length", "999", "HOST", "other");

			AssertOk(result);
			var expected = Join(
				"POST /Service.svc HTTP/1.1",
				"Host: example.com",
				"X-One: 1",
				"Content-Length: 4",
				string.Empty,
				"<a/>");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		[DataRow("X A")]
		[DataRow("X:A")]
		[DataRow("X(A)")]
		[DataRow("X/A")]
		[DataRow("X\"A")]
		[DataRow("X,A")]
		[DataRow("X;A")]
		[DataRow("X=A")]
		[DataRow("X\u00e9A")]
		public void Http_RejectsHeaderNamesThatAreNotTokens(string name)
		{
			var result = BuildSnippet("http", Url, "x", name, "v");

			AssertFailed(result);
		}

		[TestMethod]
		[DataRow("X-A")]
		[DataRow("X_A")]
		[DataRow("x.a")]
		[DataRow("X~A")]
		[DataRow("X+A")]
		[DataRow("X'A")]
		[DataRow("X#A")]
		[DataRow("SOAPAction")]
		public void Http_AcceptsHeaderNamesThatAreTokens(string name)
		{
			var result = BuildSnippet("http", Url, "x", name, "v");

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "\n" + name + ": v\n");
		}

		[TestMethod]
		public void Http_KeepsTheOrderOfTheHeaders_AndLeavesLeadingSpacesOfValuesAlone()
		{
			var result = BuildSnippet("http", Url, "x", "X-One", "1", "X-Two", " two ", "X-Three", string.Empty);

			AssertOk(result);
			var expected = Join(
				"POST /Service.svc HTTP/1.1",
				"Host: example.com",
				"X-One: 1",
				"X-Two:  two ",
				"X-Three: ",
				"Content-Length: 1",
				string.Empty,
				"x");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		// ------------------------------------------------------------------
		// buildAuthHeader
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("'abc'", "Bearer abc")]
		[DataRow("'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln-_~+/='", "Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln-_~+/=")]
		[DataRow("'a.b.c'", "Bearer a.b.c")]
		[DataRow("'!#$%&\\'*+-.^_`|~'", "Bearer !#$%&'*+-.^_`|~")]
		public void BuildAuthHeader_Bearer_BuildsAnAuthorizationHeader(string tokenSource, string expectedValue)
		{
			var result = Call("buildAuthHeader", "'bearer'", "{ token: " + tokenSource + " }");

			AssertAuthOk(result, "Authorization", expectedValue);
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("' '")]
		[DataRow("'a b'")]
		[DataRow("' abc'")]
		[DataRow("'abc '")]
		[DataRow("'a\\tb'")]
		[DataRow("'a\\nb'")]
		[DataRow("'abc\\n'")]
		[DataRow("'a\\u0000b'")]
		[DataRow("'a\\u007fb'")]
		[DataRow("'caf\\u00e9'")]
		[DataRow("'\\u65e5'")]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		[DataRow("true")]
		[DataRow("{}")]
		[DataRow("['abc']")]
		public void BuildAuthHeader_Bearer_RejectsAnEmptyOrNotPrintableToken(string tokenSource)
		{
			var result = Call("buildAuthHeader", "'bearer'", "{ token: " + tokenSource + " }");

			AssertAuthFailed(result);
		}

		[TestMethod]
		public void BuildAuthHeader_Bearer_TheHeaderValueIsLimitedTo4096Characters()
		{
			// "Bearer " takes 7 of them.
			AssertAuthOk(Call("buildAuthHeader", "'bearer'", "{ token: 'a'.repeat(4089) }"), "Authorization", "Bearer " + new string('a', 4089));
			AssertAuthFailed(Call("buildAuthHeader", "'bearer'", "{ token: 'a'.repeat(4090) }"));
		}

		[TestMethod]
		[DataRow("'Aladdin'", "'open sesame'", "QWxhZGRpbjpvcGVuIHNlc2FtZQ==")]
		[DataRow("'user'", "''", "dXNlcjo=")]
		[DataRow("'user'", "undefined", "dXNlcjo=")]
		[DataRow("'user'", "null", "dXNlcjo=")]
		[DataRow("'a'", "''", "YTo=")]
		[DataRow("'a'", "':x'", "YTo6eA==")]
		[DataRow("'user'", "'p:w'", "dXNlcjpwOnc=")]
		[DataRow("'usu\\u00e1rio'", "'p\\u00e4ss'", "dXN1w6FyaW86cMOkc3M=")]
		[DataRow("'user\\ud83d\\ude00'", "'pw\\ud83d\\ude00'", "dXNlcvCfmIA6cHfwn5iA")]
		[DataRow("'user name'", "' pw '", "dXNlciBuYW1lOiBwdyA=")]
		public void BuildAuthHeader_Basic_EncodesTheCredentialsAsUtf8Base64(string usernameSource, string passwordSource, string expectedBase64)
		{
			var result = Call("buildAuthHeader", "'basic'", "{ username: " + usernameSource + ", password: " + passwordSource + " }");

			AssertAuthOk(result, "Authorization", "Basic " + expectedBase64);
		}

		[TestMethod]
		public void BuildAuthHeader_Basic_MatchesTheDotNetEncoding()
		{
			var pairs = new[] { ("Aladdin", "open sesame"), ("user", "p@ss:w0rd"), ("J\u00fcrgen", "\u00fcber \u20ac"), ("\u65e5\u672c", "\U0001F600\U0001F680"), ("u", new string('p', 500)) };

			foreach (var (username, password) in pairs)
			{
				var result = Call("buildAuthHeader", "'basic'", "{ username: " + Lit(username) + ", password: " + Lit(password) + " }");

				AssertAuthOk(result, "Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
			}
		}

		[TestMethod]
		[DataRow("''", "'x'")]
		[DataRow("undefined", "'x'")]
		[DataRow("null", "'x'")]
		[DataRow("5", "'x'")]
		[DataRow("'a:b'", "'x'")]
		[DataRow("':'", "'x'")]
		[DataRow("'a\\nb'", "'x'")]
		[DataRow("'a\\u0000b'", "'x'")]
		[DataRow("'a\\tb'", "'x'")]
		[DataRow("'user'", "'p\\nw'")]
		[DataRow("'user'", "'p\\u0000w'")]
		[DataRow("'user'", "'p\\u007fw'")]
		[DataRow("'user'", "5")]
		[DataRow("'user'", "{}")]
		[DataRow("'user'", "true")]
		public void BuildAuthHeader_Basic_RejectsInvalidCredentials(string usernameSource, string passwordSource)
		{
			var result = Call("buildAuthHeader", "'basic'", "{ username: " + usernameSource + ", password: " + passwordSource + " }");

			AssertAuthFailed(result);
		}

		[TestMethod]
		public void BuildAuthHeader_Basic_TheHeaderValueIsLimitedTo4096Characters()
		{
			// u:ppp... is 3067 bytes at 3065 characters, which is 4092 Base64 characters and 4098 with the "Basic " prefix.
			AssertAuthOk(Call("buildAuthHeader", "'basic'", "{ username: 'u', password: 'p'.repeat(3064) }"), "Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:" + new string('p', 3064))));
			AssertAuthFailed(Call("buildAuthHeader", "'basic'", "{ username: 'u', password: 'p'.repeat(3065) }"));
		}

		[TestMethod]
		[DataRow("'X-Api-Key'", "'abc123'")]
		[DataRow("'X-Api-Key'", "'k 1'")]
		[DataRow("'api_key'", "'!#$%&\\'()*+,-./:;<=>?@[]^_`{|}~\"'")]
		[DataRow("'Ocp-Apim-Subscription-Key'", "'0123456789abcdef'")]
		[DataRow("'X1'", "'a'")]
		public void BuildAuthHeader_ApiKey_BuildsTheNamedHeader(string nameSource, string valueSource)
		{
			var result = Call("buildAuthHeader", "'apiKey'", "{ name: " + nameSource + ", value: " + valueSource + " }");

			AssertOk(result);
			var expression = "(function (name, value) { return window.SoapUiWsdl.buildAuthHeader('apiKey', { name: name, value: value }); }(" + nameSource + ", " + valueSource + "))";
			var header = SoapUiJavaScriptHost.Evaluate(expression).GetProperty("header");
			Assert.AreEqual(SoapUiJavaScriptHost.Evaluate(nameSource).GetString(), header.GetProperty("name").GetString());
			Assert.AreEqual(SoapUiJavaScriptHost.Evaluate(valueSource).GetString(), header.GetProperty("value").GetString());
		}

		[TestMethod]
		public void BuildAuthHeader_ApiKey_TrimsTheHeaderName_AndTheResultPassesValidateHeader()
		{
			var result = Call("buildAuthHeader", "'apiKey'", "{ name: '  X-Api-Key ', value: 'k' }");

			AssertAuthOk(result, "X-Api-Key", "k");
			var checkedHeader = Call("validateHeader", "'X-Api-Key'", "'k'");
			AssertOk(checkedHeader);
		}

		[TestMethod]
		[DataRow("''", "'x'")]
		[DataRow("' '", "'x'")]
		[DataRow("undefined", "'x'")]
		[DataRow("null", "'x'")]
		[DataRow("5", "'x'")]
		[DataRow("'bad name'", "'x'")]
		[DataRow("'bad:name'", "'x'")]
		[DataRow("'X-\\u00e9'", "'x'")]
		[DataRow("'Cookie'", "'x'")]
		[DataRow("'Host'", "'x'")]
		[DataRow("'Content-Type'", "'x'")]
		[DataRow("'SOAPAction'", "'x'")]
		[DataRow("'Sec-Fetch-Mode'", "'x'")]
		[DataRow("'X-Api-Key'", "''")]
		[DataRow("'X-Api-Key'", "' '")]
		[DataRow("'X-Api-Key'", "undefined")]
		[DataRow("'X-Api-Key'", "null")]
		[DataRow("'X-Api-Key'", "5")]
		[DataRow("'X-Api-Key'", "'a\\tb'")]
		[DataRow("'X-Api-Key'", "'a\\nb'")]
		[DataRow("'X-Api-Key'", "'a\\r\\nInjected: 1'")]
		[DataRow("'X-Api-Key'", "'a\\u0000b'")]
		[DataRow("'X-Api-Key'", "'a\\u007fb'")]
		[DataRow("'X-Api-Key'", "'caf\\u00e9'")]
		public void BuildAuthHeader_ApiKey_RejectsAnInvalidHeaderOrValue(string nameSource, string valueSource)
		{
			var result = Call("buildAuthHeader", "'apiKey'", "{ name: " + nameSource + ", value: " + valueSource + " }");

			AssertAuthFailed(result);
		}

		[TestMethod]
		public void BuildAuthHeader_ApiKey_TheValueIsLimitedTo4096Characters()
		{
			var accepted = Call("buildAuthHeader", "'apiKey'", "{ name: 'X-Api-Key', value: 'a'.repeat(4096) }");
			var refused = Call("buildAuthHeader", "'apiKey'", "{ name: 'X-Api-Key', value: 'a'.repeat(4097) }");

			AssertAuthOk(accepted, "X-Api-Key", new string('a', 4096));
			AssertAuthFailed(refused);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("''")]
		[DataRow("'Bearer'")]
		[DataRow("'BEARER'")]
		[DataRow("'Basic'")]
		[DataRow("'apikey'")]
		[DataRow("'digest'")]
		[DataRow("'wsUsernameToken'")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("['bearer']")]
		[DataRow("'__proto__'")]
		[DataRow("'constructor'")]
		public void BuildAuthHeader_UnknownScheme_Fails(string schemeSource)
		{
			var result = Call("buildAuthHeader", schemeSource, "{ token: 'abc', username: 'u', password: 'p', name: 'X-Key', value: 'v' }");

			AssertAuthFailed(result);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("'abc'")]
		[DataRow("5")]
		[DataRow("[]")]
		[DataRow("['abc']")]
		public void BuildAuthHeader_ValuesThatAreNotAnObject_Fail(string valuesSource)
		{
			foreach (var scheme in new[] { "bearer", "basic", "apiKey" })
			{
				AssertAuthFailed(Call("buildAuthHeader", Lit(scheme), valuesSource));
			}
		}

		// ------------------------------------------------------------------
		// utf8Bytes
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("''", "")]
		[DataRow("'abc'", "97,98,99")]
		[DataRow("'A:b'", "65,58,98")]
		[DataRow("'\\u0000'", "0")]
		[DataRow("'\\u007f'", "127")]
		[DataRow("'\\u0080'", "194,128")]
		[DataRow("'caf\\u00e9'", "99,97,102,195,169")]
		[DataRow("'\\u07ff'", "223,191")]
		[DataRow("'\\u0800'", "224,160,128")]
		[DataRow("'\\u20ac'", "226,130,172")]
		[DataRow("'\\u65e5\\u672c'", "230,151,165,230,156,172")]
		[DataRow("'\\uffff'", "239,191,191")]
		[DataRow("'\\ud83d\\ude00'", "240,159,152,128")]
		[DataRow("'a\\ud83d\\ude00b'", "97,240,159,152,128,98")]
		[DataRow("'\\ud800\\udc00'", "240,144,128,128")]
		[DataRow("'\\udbff\\udfff'", "244,143,191,191")]
		public void Utf8Bytes_EncodesCodePointsAsUtf8(string textSource, string expectedBytes)
		{
			var result = Call("utf8Bytes", textSource);

			Assert.AreEqual(expectedBytes, string.Join(",", result.EnumerateArray().Select(item => item.GetInt32())));
		}

		[TestMethod]
		[DataRow("'\\ud800'")]
		[DataRow("'\\udbff'")]
		[DataRow("'\\udc00'")]
		[DataRow("'\\udfff'")]
		public void Utf8Bytes_ALoneSurrogateBecomesTheReplacementCharacter(string textSource)
		{
			var result = Call("utf8Bytes", textSource);

			Assert.AreEqual("239,191,189", string.Join(",", result.EnumerateArray().Select(item => item.GetInt32())));
		}

		[TestMethod]
		[DataRow("'a\\ud800b'", "97,239,191,189,98")]
		[DataRow("'a\\udc00b'", "97,239,191,189,98")]
		[DataRow("'\\ud800a'", "239,191,189,97")]
		[DataRow("'\\ude00\\ud83d'", "239,191,189,239,191,189")]
		[DataRow("'\\ud800\\ud800\\udc00'", "239,191,189,240,144,128,128")]
		public void Utf8Bytes_ASurrogateThatDoesNotFormAPair_DoesNotSwallowItsNeighbour(string textSource, string expectedBytes)
		{
			var result = Call("utf8Bytes", textSource);

			Assert.AreEqual(expectedBytes, string.Join(",", result.EnumerateArray().Select(item => item.GetInt32())));
		}

		[TestMethod]
		public void Utf8Bytes_MatchesTheDotNetEncodingForAssortedTexts()
		{
			var texts = new[] { string.Empty, "plain ASCII", "caf\u00e9 cr\u00e8me", "\u0394\u03b5\u03bb\u03c4\u03b1", "\u65e5\u672c\u8a9e\u306e\u30c6\u30ad\u30b9\u30c8", "\U0001F600 and \U0001F680 and \U00010348", "<a>&\"'</a>\r\n\t", new string('\u20ac', 300) };

			foreach (var text in texts)
			{
				var result = Call("utf8Bytes", Lit(text));

				CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(text), result.EnumerateArray().Select(item => (byte)item.GetInt32()).ToArray(), text);
			}
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("['a']")]
		[DataRow("true")]
		public void Utf8Bytes_NotAString_GivesNoBytes(string valueSource)
		{
			var result = Call("utf8Bytes", valueSource);

			Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
			Assert.AreEqual(0, result.GetArrayLength());
		}

		// ------------------------------------------------------------------
		// base64Encode
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("", "")]
		[DataRow("f", "Zg==")]
		[DataRow("fo", "Zm8=")]
		[DataRow("foo", "Zm9v")]
		[DataRow("foob", "Zm9vYg==")]
		[DataRow("fooba", "Zm9vYmE=")]
		[DataRow("foobar", "Zm9vYmFy")]
		public void Base64Encode_MatchesTheRfc4648TestVectors(string text, string expected)
		{
			var result = SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.base64Encode(window.SoapUiWsdl.utf8Bytes(" + Lit(text) + "))");

			Assert.AreEqual(expected, result.GetString());
		}

		[TestMethod]
		[DataRow("[]", "")]
		[DataRow("[0]", "AA==")]
		[DataRow("[255]", "/w==")]
		[DataRow("[255, 254]", "//4=")]
		[DataRow("[0, 0, 0]", "AAAA")]
		[DataRow("[250, 251, 252]", "+vv8")]
		[DataRow("[1, 2, 3, 4]", "AQIDBA==")]
		[DataRow("[1, 2, 3, 4, 5]", "AQIDBAU=")]
		[DataRow(NonceSource, NonceBase64)]
		public void Base64Encode_EncodesBytesWithPadding(string bytesSource, string expected)
		{
			Assert.AreEqual(expected, Call("base64Encode", bytesSource).GetString());
		}

		[TestMethod]
		public void Base64Encode_MatchesTheDotNetEncodingForEveryLength()
		{
			var all = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();

			for (var length = 0; length <= 40; length++)
			{
				var bytes = all.Skip((length * 3) % 200).Take(length).ToArray();

				var result = Call("base64Encode", "[" + string.Join(", ", bytes) + "]");

				Assert.AreEqual(Convert.ToBase64String(bytes), result.GetString(), "length " + length);
			}

			Assert.AreEqual(Convert.ToBase64String(all), Call("base64Encode", "[" + string.Join(", ", all) + "]").GetString());
		}

		[TestMethod]
		[DataRow("[256]")]
		[DataRow("[-1]")]
		[DataRow("[1.5]")]
		[DataRow("['a']")]
		[DataRow("['1']")]
		[DataRow("[null]")]
		[DataRow("[undefined]")]
		[DataRow("[NaN]")]
		[DataRow("[Infinity]")]
		[DataRow("[1, 2, 300]")]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("'abc'")]
		[DataRow("{}")]
		[DataRow("5")]
		public void Base64Encode_NotAnArrayOfBytes_GivesAnEmptyString(string bytesSource)
		{
			Assert.AreEqual(string.Empty, Call("base64Encode", bytesSource).GetString());
		}

		[TestMethod]
		public void Base64Encode_AcceptsAUint8Array()
		{
			Assert.AreEqual(NonceBase64, Call("base64Encode", "new Uint8Array(" + NonceSource + ")").GetString());
		}

		// ------------------------------------------------------------------
		// wsPasswordDigest (SHA-1 and Base64, as SoapCore's WsMessageFilter computes it)
		// ------------------------------------------------------------------
		[TestMethod]
		public void WsPasswordDigest_MatchesTheDigestOfTheSoapCoreMessageFilterTest()
		{
			// The values of UseNonceAndCreatedInDigestAgainstReplayAttack in WsMessageFilterTests.
			var nonce = Convert.FromBase64String("l//4xNUs0LzslTkEA/Ch1Q==");

			var digest = Call("wsPasswordDigest", "[" + string.Join(", ", nonce) + "]", "'2020-03-06T19:58:28.134Z'", "'Password'");

			Assert.AreEqual("U1GjAqli//AHdFxRZUbVeJYz6GA=", digest.GetString());
		}

		[TestMethod]
		[DataRow("[]", "''", "'abc'", "qZk+NkcGgWq6PiVxeFDCbJzQ2J0=")]
		[DataRow("[]", "''", "''", "2jmj7l5rSw0yVb/vlWAYkK/YBwk=")]
		[DataRow("[]", "''", "'abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq'", "hJg+RBw70m66rkqh+VEp5eVGcPE=")]
		[DataRow("[]", "''", "'a'.repeat(55)", "wci73CJ5bijA4VFj0giZtlYh1lo=")]
		[DataRow("[]", "''", "'a'.repeat(56)", "wtszD2CDhUyZ1LW/tujynyAb5pk=")]
		[DataRow("[]", "''", "'a'.repeat(63)", "A/CfWxWKeoza2SC93Cm4HBilUfU=")]
		[DataRow("[]", "''", "'a'.repeat(64)", "AJi6gktcFkJ716ESKlpEKiXsZE0=")]
		[DataRow("[]", "''", "'a'.repeat(65)", "EWVTJscI1wMZviYQ6KV9mluVnTs=")]
		[DataRow("[]", "''", "'a'.repeat(119)", "7pcQZaqgF+BjKoymx3uzv4sd/FY=")]
		[DataRow("[]", "''", "'a'.repeat(120)", "80wUiDhTRqVXCboFbd0IKA3UxtY=")]
		[DataRow("[]", "''", "'pass'", "nU4eI71bcnBGqeO0t9tXvY1u5oQ=")]
		[DataRow("[]", "'pa'", "'ss'", "nU4eI71bcnBGqeO0t9tXvY1u5oQ=")]
		[DataRow(NonceSource, "'2025-01-31T12:00:00Z'", "'pass'", "ic4ELejAaq+LWm9+Z69+jAcCZSc=")]
		[DataRow(NonceSource, "'2025-01-31T12:00:00Z'", "'s3cret'", "I2KIVghb+fcjtW1dKWvrcfFhEf4=")]
		[DataRow(NonceSource, "'2025-01-31T12:00:00Z'", "'p\\u00e4ss\\ud83d\\ude00'", "nQNv2ffJEmWITNElRQ3CcBynFjo=")]
		public void WsPasswordDigest_IsTheBase64OfTheSha1OfNonceCreatedAndPassword(string nonceSource, string createdSource, string passwordSource, string expected)
		{
			var digest = Call("wsPasswordDigest", nonceSource, createdSource, passwordSource);

			Assert.AreEqual(expected, digest.GetString());
		}

		[TestMethod]
		public void WsPasswordDigest_MatchesTheDotNetComputationForAssortedInputs()
		{
			var nonces = new[] { Array.Empty<byte>(), new byte[] { 1 }, Enumerable.Range(0, 16).Select(value => (byte)(value * 17)).ToArray(), Enumerable.Range(0, 70).Select(value => (byte)(255 - value)).ToArray() };
			var createds = new[] { string.Empty, Created, "2020-03-06T19:58:28.134Z" };
			var passwords = new[] { string.Empty, "secret", "p\u00e4ss w\u00f6rd", "\U0001F600 \u65e5\u672c", new string('x', 1000) };

			foreach (var nonce in nonces)
			{
				foreach (var created in createds)
				{
					foreach (var password in passwords)
					{
						var data = nonce.Concat(Encoding.UTF8.GetBytes(created)).Concat(Encoding.UTF8.GetBytes(password)).ToArray();
						var expected = Convert.ToBase64String(SHA1.HashData(data));

						var digest = Call("wsPasswordDigest", "[" + string.Join(", ", nonce) + "]", Lit(created), Lit(password));

						Assert.AreEqual(expected, digest.GetString(), nonce.Length + " nonce bytes, created '" + created + "', password of " + password.Length + " characters");
					}
				}
			}
		}

		[TestMethod]
		public void WsPasswordDigest_AcceptsAUint8ArrayAsTheNonce()
		{
			var fromArray = Call("wsPasswordDigest", NonceSource, "'" + Created + "'", "'pass'");
			var fromTypedArray = Call("wsPasswordDigest", "new Uint8Array(" + NonceSource + ")", "'" + Created + "'", "'pass'");

			Assert.AreEqual("ic4ELejAaq+LWm9+Z69+jAcCZSc=", fromTypedArray.GetString());
			Assert.AreEqual(fromArray.GetString(), fromTypedArray.GetString());
		}

		[TestMethod]
		[DataRow("undefined", "'c'", "'p'")]
		[DataRow("null", "'c'", "'p'")]
		[DataRow("'abc'", "'c'", "'p'")]
		[DataRow("{}", "'c'", "'p'")]
		[DataRow("[256]", "'c'", "'p'")]
		[DataRow("[-1]", "'c'", "'p'")]
		[DataRow("[1.5]", "'c'", "'p'")]
		[DataRow("['1']", "'c'", "'p'")]
		[DataRow("[]", "undefined", "'p'")]
		[DataRow("[]", "null", "'p'")]
		[DataRow("[]", "5", "'p'")]
		[DataRow("[]", "'c'", "undefined")]
		[DataRow("[]", "'c'", "null")]
		[DataRow("[]", "'c'", "5")]
		public void WsPasswordDigest_InvalidInput_GivesAnEmptyString(string nonceSource, string createdSource, string passwordSource)
		{
			var digest = Call("wsPasswordDigest", nonceSource, createdSource, passwordSource);

			Assert.AreEqual(JsonValueKind.String, digest.ValueKind);
			Assert.AreEqual(string.Empty, digest.GetString());
		}

		// ------------------------------------------------------------------
		// buildWsSecurityHeader
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildWsSecurityHeader_Text_BuildsAUsernameTokenWithAPlainPassword()
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: false, includeCreated: false, redact: false }", "'" + Created + "'", NonceSource);

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "secret")));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_Text_CanIncludeNonceAndCreated_InThatOrderAfterThePassword()
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: true, includeCreated: true }", "'" + Created + "'", NonceSource);

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "secret") + NonceElement() + CreatedElement()));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_Text_CanIncludeJustTheNonceOrJustCreated()
		{
			var onlyNonce = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: true, includeCreated: false }", "undefined", NonceSource);
			var onlyCreated = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: false, includeCreated: true }", "'" + Created + "'", "undefined");

			AssertXml(onlyNonce, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "secret") + NonceElement()));
			AssertXml(onlyCreated, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "secret") + CreatedElement()));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_Digest_ComputesTheDigestOverTheNonceAndCreatedValuesItWrites()
		{
			var result = BuildWs("{ username: 'alice', password: 'pass', passwordType: 'digest', includeNonce: true, includeCreated: true, redact: false }", "'" + Created + "'", NonceSource);

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordDigestType, "ic4ELejAaq+LWm9+Z69+jAcCZSc=") + NonceElement() + CreatedElement()));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_Digest_IsTheDigestSoapCoreComputes()
		{
			var nonce = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
			var data = nonce.Concat(Encoding.UTF8.GetBytes(Created)).Concat(Encoding.UTF8.GetBytes("p\u00e4ss<&>")).ToArray();
			var expectedDigest = Convert.ToBase64String(SHA1.HashData(data));

			var result = BuildWs("{ username: 'alice', password: 'p\\u00e4ss<&>', passwordType: 'digest' }", "'" + Created + "'", NonceSource);

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordDigestType, expectedDigest) + NonceElement() + CreatedElement()));
		}

		[TestMethod]
		[DataRow("{ username: 'alice', password: 'pass', passwordType: 'digest' }")]
		[DataRow("{ username: 'alice', password: 'pass', passwordType: 'digest', includeNonce: false, includeCreated: false }")]
		[DataRow("{ username: 'alice', password: 'pass', passwordType: 'digest', includeNonce: true, includeCreated: false }")]
		[DataRow("{ username: 'alice', password: 'pass', passwordType: 'digest', includeNonce: false, includeCreated: true }")]
		public void BuildWsSecurityHeader_Digest_AlwaysIncludesNonceAndCreated_BecauseSoapCoreNeedsBoth(string optionsSource)
		{
			var result = BuildWs(optionsSource, "'" + Created + "'", NonceSource);

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordDigestType, "ic4ELejAaq+LWm9+Z69+jAcCZSc=") + NonceElement() + CreatedElement()));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_Redact_ReplacesThePasswordTextWithRedacted_AndComputesNoDigest()
		{
			var text = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', redact: true }", "'" + Created + "'", NonceSource);
			var digest = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'digest', redact: true }", "'" + Created + "'", NonceSource);

			AssertXml(text, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "REDACTED")));
			AssertXml(digest, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordDigestType, "REDACTED") + NonceElement() + CreatedElement()));
			Assert.IsFalse(text.GetProperty("xml").GetString().Contains("secret"), "The password leaked into the redacted header.");
			Assert.IsFalse(digest.GetProperty("xml").GetString().Contains("secret"), "The password leaked into the redacted header.");
		}

		[TestMethod]
		public void BuildWsSecurityHeader_Redact_DoesNotLeakAnyPartOfThePasswordOrItsDigest()
		{
			var plain = BuildWs("{ username: 'alice', password: 'Tr0ub4dor&3', passwordType: 'digest', redact: false }", "'" + Created + "'", NonceSource);
			var redacted = BuildWs("{ username: 'alice', password: 'Tr0ub4dor&3', passwordType: 'digest', redact: true }", "'" + Created + "'", NonceSource);

			AssertOk(plain);
			AssertOk(redacted);
			var digest = Call("wsPasswordDigest", NonceSource, "'" + Created + "'", "'Tr0ub4dor&3'").GetString();
			StringAssert.Contains(plain.GetProperty("xml").GetString(), digest);
			Assert.IsFalse(redacted.GetProperty("xml").GetString().Contains(digest), "The digest leaked into the redacted header.");
			Assert.IsFalse(redacted.GetProperty("xml").GetString().Contains("Tr0ub4dor"), "The password leaked into the redacted header.");
		}

		[TestMethod]
		public void BuildWsSecurityHeader_AnEmptyPasswordWithTheTextType_LeavesThePasswordElementOut()
		{
			var empty = BuildWs("{ username: 'alice', password: '', passwordType: 'text' }", "undefined", "undefined");
			var missing = BuildWs("{ username: 'alice', passwordType: 'text' }", "undefined", "undefined");
			var redacted = BuildWs("{ username: 'alice', password: '', passwordType: 'text', redact: true }", "undefined", "undefined");

			var expected = Token("<wsse:Username>alice</wsse:Username>");
			AssertXml(empty, expected);
			AssertXml(missing, expected);
			AssertXml(redacted, expected);
		}

		[TestMethod]
		public void BuildWsSecurityHeader_EscapesTheUsernameAndThePassword()
		{
			var result = BuildWs("{ username: '<a&b>\"c\\'d', password: '<p&w>\"x\\'y', passwordType: 'text' }", "undefined", "undefined");

			AssertXml(result, Token("<wsse:Username>&lt;a&amp;b&gt;&quot;c&#39;d</wsse:Username>" + PasswordElement(PasswordTextType, "&lt;p&amp;w&gt;&quot;x&#39;y")));
		}

		[TestMethod]
		[DataRow("</wsse:Username><wsse:Injected/>")]
		[DataRow("]]><x/>")]
		[DataRow("&lt;")]
		[DataRow("a\"b' onload=\"x")]
		public void BuildWsSecurityHeader_MarkupInTheCredentialsStaysText(string text)
		{
			var result = BuildWs("{ username: " + Lit(text) + ", password: " + Lit(text) + ", passwordType: 'text' }", "undefined", "undefined");

			AssertOk(result);
			var xml = result.GetProperty("xml").GetString();
			var escaped = Escape(text);
			StringAssert.Contains(xml, "<wsse:Username>" + escaped + "</wsse:Username>");
			StringAssert.Contains(xml, ">" + escaped + "</wsse:Password>");
			Assert.IsFalse(xml.Contains("<wsse:Injected"), "The text became markup: " + xml);
			Assert.AreEqual(1, Occurrences(xml, "<wsse:Username>"), xml);
			Assert.AreEqual(1, Occurrences(xml, "</wsse:UsernameToken>"), xml);
		}

		[TestMethod]
		public void BuildWsSecurityHeader_KeepsNonAsciiCredentialsAsTheyAre()
		{
			var result = BuildWs("{ username: 'usu\\u00e1rio \\u65e5\\u672c', password: 'p\\u00e4ss \\ud83d\\ude00', passwordType: 'text' }", "undefined", "undefined");

			AssertXml(result, Token("<wsse:Username>usu\u00e1rio \u65e5\u672c</wsse:Username>" + PasswordElement(PasswordTextType, "p\u00e4ss \U0001F600")));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_TheHeaderIsOneLineThatDeclaresBothPrefixes()
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'digest' }", "'" + Created + "'", NonceSource);

			AssertOk(result);
			var xml = result.GetProperty("xml").GetString();
			Assert.IsFalse(xml.Contains('\n') || xml.Contains('\r'), "The header must be a single line: " + xml);
			Assert.IsTrue(xml.StartsWith("<wsse:Security xmlns:wsse=\"" + WsseNamespace + "\" xmlns:wsu=\"" + WsuNamespace + "\">", StringComparison.Ordinal), xml);
			Assert.IsTrue(xml.EndsWith("</wsse:Security>", StringComparison.Ordinal), xml);
			StringAssert.Contains(xml, "<wsse:UsernameToken wsu:Id=\"UsernameToken-1\">");
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("'text'")]
		[DataRow("5")]
		[DataRow("[]")]
		[DataRow("[{ username: 'a' }]")]
		public void BuildWsSecurityHeader_OptionsThatAreNotAnObject_Fail(string optionsSource)
		{
			AssertXmlFailed(BuildWs(optionsSource, "'" + Created + "'", NonceSource));
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("''")]
		[DataRow("'Text'")]
		[DataRow("'DIGEST'")]
		[DataRow("'plain'")]
		[DataRow("'PasswordDigest'")]
		[DataRow("true")]
		[DataRow("5")]
		public void BuildWsSecurityHeader_ThePasswordTypeMustBeTextOrDigest(string typeSource)
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: " + typeSource + " }", "'" + Created + "'", NonceSource);

			AssertXmlFailed(result);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("''")]
		[DataRow("' '")]
		[DataRow("'  \\t '")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("'a\\nb'")]
		[DataRow("'a\\tb'")]
		[DataRow("'a\\u0000b'")]
		[DataRow("'a\\u007fb'")]
		[DataRow("'a\\ud800b'")]
		[DataRow("'a\\udc00b'")]
		[DataRow("'a\\uffffb'")]
		[DataRow("'a\\ufffeb'")]
		[DataRow("'a'.repeat(257)")]
		public void BuildWsSecurityHeader_RejectsAnInvalidUsername(string usernameSource)
		{
			foreach (var type in new[] { "text", "digest" })
			{
				var result = BuildWs("{ username: " + usernameSource + ", password: 'secret', passwordType: '" + type + "' }", "'" + Created + "'", NonceSource);

				AssertXmlFailed(result);
			}
		}

		[TestMethod]
		public void BuildWsSecurityHeader_TheUsernameIsLimitedTo256Characters()
		{
			var accepted = BuildWs("{ username: 'a'.repeat(256), password: 'secret', passwordType: 'text' }", "undefined", "undefined");

			AssertXml(accepted, Token("<wsse:Username>" + new string('a', 256) + "</wsse:Username>" + PasswordElement(PasswordTextType, "secret")));
		}

		[TestMethod]
		[DataRow("'a\\nb'")]
		[DataRow("'a\\tb'")]
		[DataRow("'a\\r\\nb'")]
		[DataRow("'a\\u0000b'")]
		[DataRow("'a\\u001bb'")]
		[DataRow("'a\\u007fb'")]
		[DataRow("'a\\ud800b'")]
		[DataRow("'a\\udc00b'")]
		[DataRow("'a\\ufffeb'")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("true")]
		[DataRow("'a'.repeat(1025)")]
		public void BuildWsSecurityHeader_RejectsAnInvalidPassword(string passwordSource)
		{
			foreach (var type in new[] { "text", "digest" })
			{
				var result = BuildWs("{ username: 'alice', password: " + passwordSource + ", passwordType: '" + type + "' }", "'" + Created + "'", NonceSource);

				AssertXmlFailed(result);
			}
		}

		[TestMethod]
		public void BuildWsSecurityHeader_ThePasswordIsLimitedTo1024Characters()
		{
			var accepted = BuildWs("{ username: 'alice', password: 'p'.repeat(1024), passwordType: 'text' }", "undefined", "undefined");

			AssertXml(accepted, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, new string('p', 1024))));
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("undefined")]
		[DataRow("null")]
		public void BuildWsSecurityHeader_TheDigestTypeNeedsAPassword(string passwordSource)
		{
			var result = BuildWs("{ username: 'alice', password: " + passwordSource + ", passwordType: 'digest' }", "'" + Created + "'", NonceSource);

			AssertXmlFailed(result);
		}

		[TestMethod]
		[DataRow("[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]")]
		[DataRow("[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]")]
		[DataRow("[]")]
		[DataRow("[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 256]")]
		[DataRow("[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, -1]")]
		[DataRow("[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 1.5]")]
		[DataRow("[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, '15']")]
		[DataRow("'0123456789abcdef'")]
		[DataRow("{}")]
		[DataRow("null")]
		[DataRow("undefined")]
		public void BuildWsSecurityHeader_AnInvalidNonce_FailsWhenItIsUsed(string nonceSource)
		{
			var digest = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'digest' }", "'" + Created + "'", nonceSource);
			var text = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: true, includeCreated: true }", "'" + Created + "'", nonceSource);

			AssertXmlFailed(digest);
			AssertXmlFailed(text);
		}

		[TestMethod]
		public void BuildWsSecurityHeader_ANonceThatIsNotUsedIsNotChecked()
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: false, includeCreated: true }", "'" + Created + "'", "'garbage'");

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "secret") + CreatedElement()));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_AcceptsAUint8ArrayAsTheNonce()
		{
			var result = BuildWs("{ username: 'alice', password: 'pass', passwordType: 'digest' }", "'" + Created + "'", "new Uint8Array(" + NonceSource + ")");

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordDigestType, "ic4ELejAaq+LWm9+Z69+jAcCZSc=") + NonceElement() + CreatedElement()));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_TheNonceElementCarriesTheBase64OfTheBytes()
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: true }", "undefined", "[255, 254, 253, 0, 1, 2, 128, 127, 64, 32, 16, 8, 4, 2, 1, 0]");

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("xml").GetString(), "<wsse:Nonce EncodingType=\"" + Base64BinaryType + "\">//79AAECgH9AIBAIBAIBAA==</wsse:Nonce>");
		}

		[TestMethod]
		[DataRow("'2025-01-31T12:00:00Z'", true)]
		[DataRow("'1999-12-31T23:59:59Z'", true)]
		[DataRow("'2025-02-28T00:00:00Z'", true)]
		[DataRow("'2025-01-31T12:00:00.123Z'", false)]
		[DataRow("'2025-01-31T12:00:00+00:00'", false)]
		[DataRow("'2025-01-31T12:00:00'", false)]
		[DataRow("'2025-01-31 12:00:00Z'", false)]
		[DataRow("'2025-1-31T12:00:00Z'", false)]
		[DataRow("'2025-01-31T12:00Z'", false)]
		[DataRow("'2025-13-01T12:00:00Z'", false)]
		[DataRow("'2025-00-10T12:00:00Z'", false)]
		[DataRow("'2025-01-32T12:00:00Z'", false)]
		[DataRow("'2025-01-00T12:00:00Z'", false)]
		[DataRow("'2025-01-31T24:00:00Z'", false)]
		[DataRow("'2025-01-31T12:60:00Z'", false)]
		[DataRow("'2025-01-31T12:00:60Z'", false)]
		[DataRow("' 2025-01-31T12:00:00Z'", false)]
		[DataRow("'2025-01-31T12:00:00Z '", false)]
		[DataRow("'2025-01-31T12:00:00Z\\n'", false)]
		[DataRow("'2025-01-31T12:00:00Z<x/>'", false)]
		[DataRow("''", false)]
		[DataRow("undefined", false)]
		[DataRow("null", false)]
		[DataRow("20250131", false)]
		[DataRow("new Date(0)", false)]
		public void BuildWsSecurityHeader_TheCreationTimeMustBeUtcWithoutFractions(string nowSource, bool accepted)
		{
			var digest = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'digest' }", nowSource, NonceSource);
			var text = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeCreated: true }", nowSource, "undefined");

			Assert.AreEqual(accepted, digest.GetProperty("ok").GetBoolean(), "digest: " + digest);
			Assert.AreEqual(accepted, text.GetProperty("ok").GetBoolean(), "text: " + text);
		}

		[TestMethod]
		public void BuildWsSecurityHeader_TheCreationTimeIsNotCheckedWhenItIsNotUsed()
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeCreated: false }", "'not a date'", "undefined");

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "secret")));
		}

		[TestMethod]
		public void BuildWsSecurityHeader_OnlyStrictTrueTurnsTheFlagsOn()
		{
			var result = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text', includeNonce: 'true', includeCreated: 1, redact: 'yes' }", "'" + Created + "'", NonceSource);

			AssertXml(result, Token("<wsse:Username>alice</wsse:Username>" + PasswordElement(PasswordTextType, "secret")));
		}

		// ------------------------------------------------------------------
		// applyWsSecurity (the DOM based part runs in a browser, only the checks that come before it run here)
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("''")]
		[DataRow("'1.0'")]
		[DataRow("'1'")]
		[DataRow("'2.0'")]
		[DataRow("5")]
		public void ApplyWsSecurity_UnsupportedVersion_Fails(string versionSource)
		{
			var result = Call("applyWsSecurity", Lit("<a/>"), Lit("<b/>"), versionSource);

			AssertTextFailed(result);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("''")]
		[DataRow("'   '")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("'plain text'")]
		[DataRow("'<!DOCTYPE a><a/>'")]
		[DataRow("'<!ENTITY x \"y\"><a/>'")]
		public void ApplyWsSecurity_AnEnvelopeThatCannotBeRead_Fails(string envelopeSource)
		{
			var result = Call("applyWsSecurity", envelopeSource, Lit("<wsse:Security xmlns:wsse=\"" + WsseNamespace + "\"/>"), "'1.1'");

			AssertTextFailed(result);
		}

		[TestMethod]
		public void ApplyWsSecurity_WithoutADom_ReturnsAFailureInsteadOfThrowing()
		{
			var envelope = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body/></soapenv:Envelope>";
			var security = BuildWs("{ username: 'alice', password: 'secret', passwordType: 'text' }", "undefined", "undefined").GetProperty("xml").GetString();

			var result = Call("applyWsSecurity", Lit(envelope), Lit(security), "'1.1'");

			AssertTextFailed(result);
		}

		// ------------------------------------------------------------------
		// sampleForClrType
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("System.String", "string")]
		[DataRow("System.Boolean", "true")]
		[DataRow("System.Int16", "1")]
		[DataRow("System.Int32", "1")]
		[DataRow("System.Int64", "1")]
		[DataRow("System.UInt16", "1")]
		[DataRow("System.UInt32", "1")]
		[DataRow("System.UInt64", "1")]
		[DataRow("System.Byte", "1")]
		[DataRow("System.SByte", "1")]
		[DataRow("System.Single", "1.5")]
		[DataRow("System.Double", "1.5")]
		[DataRow("System.Decimal", "1.5")]
		[DataRow("System.DateTime", "2025-01-01T12:00:00Z")]
		[DataRow("System.DateTimeOffset", "2025-01-01T12:00:00+00:00")]
		[DataRow("System.TimeSpan", "PT1H")]
		[DataRow("System.Guid", "00000000-0000-0000-0000-000000000000")]
		[DataRow("System.Char", "65")]
		[DataRow("System.Byte[]", "AAAA")]
		[DataRow("System.Uri", "https://example.com")]
		public void SampleForClrType_KnownTypes_GiveASampleWithoutANote(string typeName, string expectedText)
		{
			var result = Call("sampleForClrType", Lit(typeName));

			AssertSample(result, expectedText, string.Empty);
		}

		[TestMethod]
		[DataRow("System.Nullable<System.Int32>", "1")]
		[DataRow("System.Nullable<System.Boolean>", "true")]
		[DataRow("System.Nullable<System.Guid>", "00000000-0000-0000-0000-000000000000")]
		[DataRow("System.Nullable<System.DateTime>", "2025-01-01T12:00:00Z")]
		[DataRow("System.Nullable<System.DateTimeOffset>", "2025-01-01T12:00:00+00:00")]
		[DataRow("System.Nullable<System.TimeSpan>", "PT1H")]
		[DataRow("System.Nullable<System.Decimal>", "1.5")]
		[DataRow("System.Nullable< System.Int32 >", "1")]
		public void SampleForClrType_Nullable_GivesTheSampleOfTheTypeBehindIt(string typeName, string expectedText)
		{
			var result = Call("sampleForClrType", Lit(typeName));

			AssertSample(result, expectedText, string.Empty);
		}

		[TestMethod]
		[DataRow("System.String[]", "string")]
		[DataRow("System.Int32[]", "1")]
		[DataRow("System.Guid[]", "00000000-0000-0000-0000-000000000000")]
		[DataRow("System.Nullable<System.Int32>[]", "1")]
		[DataRow("System.Byte[][]", "AAAA")]
		[DataRow("System.Int32[][]", "1")]
		[DataRow("System.Collections.Generic.List<System.String>", "string")]
		[DataRow("System.Collections.Generic.IList<System.Int32>", "1")]
		[DataRow("System.Collections.Generic.IEnumerable<System.Boolean>", "true")]
		[DataRow("System.Collections.Generic.ICollection<System.Double>", "1.5")]
		[DataRow("System.Collections.Generic.List<System.Guid>", "00000000-0000-0000-0000-000000000000")]
		[DataRow("System.Collections.Generic.List<System.Nullable<System.Int32>>", "1")]
		[DataRow("System.Collections.Generic.List<System.Int32[]>", "1")]
		[DataRow("System.Collections.Generic.List<System.Byte[]>", "AAAA")]
		[DataRow("System.Collections.Generic.IEnumerable<System.Collections.Generic.List<System.String>>", "string")]
		public void SampleForClrType_ArraysAndLists_GiveTheSampleOfTheItemTypeMarkedRepeatable(string typeName, string expectedText)
		{
			var result = Call("sampleForClrType", Lit(typeName));

			AssertSample(result, expectedText, "repeatable");
		}

		[TestMethod]
		public void SampleForClrType_ByteArrayIsASingleValueNotAList()
		{
			AssertSample(Call("sampleForClrType", "'System.Byte[]'"), "AAAA", string.Empty);
			AssertSample(Call("sampleForClrType", "'System.Byte[][]'"), "AAAA", "repeatable");
		}

		[TestMethod]
		[DataRow("")]
		[DataRow("   ")]
		[DataRow("Weather.Models.Order")]
		[DataRow("System.Object")]
		[DataRow("System.Void")]
		[DataRow("System.Xml.Linq.XElement")]
		[DataRow("System.Collections.Generic.Dictionary<System.String, System.Int32>")]
		[DataRow("System.Collections.Generic.Dictionary<System.String,System.Int32>")]
		[DataRow("System.Collections.Generic.List<Weather.Models.Order>")]
		[DataRow("System.Collections.Generic.List<System.Collections.Generic.Dictionary<System.String, System.Int32>>")]
		[DataRow("System.Nullable<Weather.Models.Kind>")]
		[DataRow("System.Nullable<System.Int32, System.Int32>")]
		[DataRow("System.Nullable<>")]
		[DataRow("System.Collections.Generic.List<>")]
		[DataRow("System.Collections.Generic.List<System.Int32")]
		[DataRow("System.Collections.Generic.HashSet<System.Int32>")]
		[DataRow("List<System.Int32>")]
		[DataRow("Nullable<System.Int32>")]
		[DataRow("system.int32")]
		[DataRow("System.int32")]
		[DataRow("SYSTEM.STRING")]
		[DataRow("string")]
		[DataRow("int")]
		[DataRow("System.Int32?")]
		[DataRow("System.Int32[,]")]
		[DataRow("System.Int32[")]
		[DataRow("[]")]
		[DataRow("Weather.Models.Order[]")]
		[DataRow("System.Int32&")]
		[DataRow("System.Int32*")]
		public void SampleForClrType_UnknownOrComplexTypes_GiveNull(string typeName)
		{
			var result = Call("sampleForClrType", Lit(typeName));

			Assert.AreEqual(JsonValueKind.Null, result.ValueKind, typeName);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		[DataRow("true")]
		[DataRow("{}")]
		[DataRow("['System.String']")]
		[DataRow("{ toString: function () { return 'System.String'; } }")]
		public void SampleForClrType_NotAString_GivesNull(string typeSource)
		{
			var result = Call("sampleForClrType", typeSource);

			Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
		}

		[TestMethod]
		public void SampleForClrType_IgnoresSpacesAroundTheName_AndGivesNullForAnOverlongName()
		{
			AssertSample(Call("sampleForClrType", "'  System.Int32  '"), "1", string.Empty);
			Assert.AreEqual(JsonValueKind.Null, Call("sampleForClrType", "'System.' + 'a'.repeat(600)").ValueKind);
		}

		[TestMethod]
		public void SampleForClrType_ADeeplyNestedTypeDoesNotOverflowTheStack()
		{
			var nested = "'System.Collections.Generic.List<'.repeat(200) + 'System.Int32' + '>'.repeat(200)";

			var result = Call("sampleForClrType", nested);

			Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
		}

		[TestMethod]
		public void SampleForClrType_SamplesAreAcceptableValuesForTheirTypes()
		{
			Assert.IsTrue(DateTimeOffset.TryParse("2025-01-01T12:00:00Z", out _));
			Assert.IsTrue(DateTimeOffset.TryParse("2025-01-01T12:00:00+00:00", out _));
			Assert.IsTrue(Guid.TryParse("00000000-0000-0000-0000-000000000000", out _));
			Assert.IsTrue(System.Xml.XmlConvert.ToTimeSpan("PT1H") == TimeSpan.FromHours(1));
			Assert.IsTrue(Uri.TryCreate("https://example.com", UriKind.Absolute, out _));
		}

		private static JsonElement Call(string function, params string[] argumentsSource)
		{
			return SoapUiJavaScriptHost.Call(function, argumentsSource);
		}

		private static string Lit(string value)
		{
			return SoapUiJavaScriptHost.Literal(value);
		}

		private static string[] Strings(JsonElement array)
		{
			return array.EnumerateArray().Select(item => item.GetString()).ToArray();
		}

		// Joins the lines with LF and ends the text with a line break, the way the PowerShell and C# snippets end.
		private static string Lines(params string[] lines)
		{
			return Join(lines) + "\n";
		}

		private static string Join(params string[] lines)
		{
			return string.Join("\n", lines);
		}

		// headerPairs holds name and value pairs: name1, value1, name2, value2. Returns JavaScript source.
		private static string RequestSource(string url, string body, params string[] headerPairs)
		{
			if (headerPairs.Length % 2 != 0)
			{
				throw new ArgumentException("Headers are given as name and value pairs.", nameof(headerPairs));
			}

			var headers = new List<string>();
			for (var index = 0; index < headerPairs.Length; index += 2)
			{
				headers.Add("{ name: " + Lit(headerPairs[index]) + ", value: " + Lit(headerPairs[index + 1]) + " }");
			}

			return "{ url: " + Lit(url) + ", headers: [" + string.Join(", ", headers) + "], body: " + Lit(body) + " }";
		}

		private static JsonElement BuildSnippet(string kind, string url, string body, params string[] headerPairs)
		{
			return Call("buildSnippet", Lit(kind), RequestSource(url, body, headerPairs));
		}

		// The line (or the part of the snippet) that carries a header, for the kind of snippet.
		private static string HeaderLine(string kind, string name, string value)
		{
			switch (kind)
			{
				case "curl":
					return "-H '" + name + ": " + value + "'";
				case "powershell":
					return "'" + name + "' = '" + value + "'";
				case "csharp":
					return "TryAddWithoutValidation(\"" + name + "\", \"" + value + "\")";
				default:
					return "\n" + name + ": " + value + "\n";
			}
		}

		private static JsonElement BuildWs(string optionsSource, string nowSource, string nonceSource)
		{
			return Call("buildWsSecurityHeader", optionsSource, nowSource, nonceSource);
		}

		private static string Token(string inner)
		{
			return "<wsse:Security xmlns:wsse=\"" + WsseNamespace + "\" xmlns:wsu=\"" + WsuNamespace + "\"><wsse:UsernameToken wsu:Id=\"UsernameToken-1\">" + inner + "</wsse:UsernameToken></wsse:Security>";
		}

		private static string PasswordElement(string type, string escapedText)
		{
			return "<wsse:Password Type=\"" + type + "\">" + escapedText + "</wsse:Password>";
		}

		private static string NonceElement()
		{
			return "<wsse:Nonce EncodingType=\"" + Base64BinaryType + "\">" + NonceBase64 + "</wsse:Nonce>";
		}

		private static string CreatedElement()
		{
			return "<wsu:Created>" + Created + "</wsu:Created>";
		}

		private static string Escape(string text)
		{
			return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
		}

		private static int Occurrences(string text, string part)
		{
			return text.Split(new[] { part }, StringSplitOptions.None).Length - 1;
		}

		private static void AssertOk(JsonElement result)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected an object but got: " + result);
			Assert.IsTrue(result.GetProperty("ok").GetBoolean(), "Expected ok but got: " + result);
		}

		private static void AssertFailed(JsonElement result)
		{
			AssertFailure(result, "text");
		}

		private static void AssertTextFailed(JsonElement result)
		{
			AssertFailure(result, "text");
		}

		private static void AssertAuthFailed(JsonElement result)
		{
			AssertFailure(result, "header");
		}

		private static void AssertXmlFailed(JsonElement result)
		{
			AssertFailure(result, "xml");
		}

		private static void AssertFailure(JsonElement result, string payloadProperty)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected an object but got: " + result);
			Assert.IsFalse(result.GetProperty("ok").GetBoolean(), "Expected a failure but got: " + result);
			Assert.AreEqual(JsonValueKind.String, result.GetProperty("error").ValueKind, "A failure needs an error message: " + result);
			Assert.IsFalse(string.IsNullOrWhiteSpace(result.GetProperty("error").GetString()), "A failure needs an error message: " + result);
			Assert.IsFalse(result.TryGetProperty(payloadProperty, out _), "A failure must not carry any " + payloadProperty + ": " + result);
		}

		private static void AssertAuthOk(JsonElement result, string name, string value)
		{
			AssertOk(result);
			var header = result.GetProperty("header");
			Assert.AreEqual(name, header.GetProperty("name").GetString());
			Assert.AreEqual(value, header.GetProperty("value").GetString());
		}

		private static void AssertXml(JsonElement result, string expectedXml)
		{
			AssertOk(result);
			Assert.AreEqual(expectedXml, result.GetProperty("xml").GetString());
		}

		private static void AssertSample(JsonElement result, string text, string note)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected a sample but got: " + result);
			Assert.AreEqual(text, result.GetProperty("text").GetString());
			Assert.AreEqual(note, result.GetProperty("note").GetString());
		}
	}
}
