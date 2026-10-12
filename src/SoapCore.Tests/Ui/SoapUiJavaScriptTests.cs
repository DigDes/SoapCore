using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.Ui
{
	// Unit tests of the explorer's page independent JavaScript (wsdl-parser.js), run in the Jint interpreter.
	// Only the functions that need no DOM are covered. The ones that parse or build XML need a browser.
	// Test data written as JavaScript source (parameters named ...Source) keeps control characters out of the test names.
	// The script still receives the real characters.
	[TestClass]
	public class SoapUiJavaScriptTests
	{
		private const string SoapAction = "http://tempuri.org/ISampleService/Ping";

		private const string CurlUrl = "https://example.com/Service.svc";

		private const string ExportedFunctions = "['parse', 'buildEnvelope', 'fallbackEnvelope', 'prettyPrintXml', 'escapeXml', 'parseSoapFault', 'faultFromText', 'hasDoctype', 'envelopeNamespace', 'protocolHeaders', 'validateEnvelope', 'validateAction', 'validateHeader', 'isSensitiveHeader', 'shellQuote', 'shortenSoapAction', 'formatBytes', 'buildHashRoute', 'parseHashRoute', 'buildSvcutilCommand', 'buildSnippet', 'buildAuthHeader', 'wsPasswordDigest', 'utf8Bytes', 'base64Encode', 'buildWsSecurityHeader', 'applyWsSecurity', 'sampleForClrType', 'buildFormTemplate', 'inputKindForType', 'validateXsdValue', 'sampleValueForType', 'tokenizeXml', 'findMatches', 'buildWsdlOutline']";

		// ------------------------------------------------------------------
		// Surface
		// ------------------------------------------------------------------
		[TestMethod]
		public void Script_LoadsWithoutADom_AndPublishesTheApi()
		{
			var kind = SoapUiJavaScriptHost.Evaluate("typeof window.SoapUiWsdl");

			Assert.AreEqual("object", kind.GetString());
		}

		[TestMethod]
		public void Surface_IsFrozen()
		{
			Assert.IsTrue(SoapUiJavaScriptHost.Evaluate("Object.isFrozen(window.SoapUiWsdl)").GetBoolean(), "window.SoapUiWsdl must be frozen.");
			Assert.IsTrue(SoapUiJavaScriptHost.Evaluate("Object.isFrozen(window.SoapUiWsdl.curl)").GetBoolean(), "window.SoapUiWsdl.curl must be frozen.");
		}

		[TestMethod]
		public void Surface_ExposesEveryFunctionTheExplorerUses()
		{
			var expression = "(function (names) { return names.filter(function (name) { return typeof window.SoapUiWsdl[name] !== 'function'; }); }(" + ExportedFunctions + "))";

			var missing = Strings(SoapUiJavaScriptHost.Evaluate(expression));

			Assert.AreEqual(0, missing.Length, "Missing functions on window.SoapUiWsdl: " + string.Join(", ", missing));
			Assert.AreEqual("function", SoapUiJavaScriptHost.Evaluate("typeof window.SoapUiWsdl.curl.build").GetString());
			Assert.IsTrue(SoapUiJavaScriptHost.Evaluate("Array.isArray(window.SoapUiWsdl.snippetKinds)").GetBoolean(), "window.SoapUiWsdl.snippetKinds must be an array.");
		}

		[TestMethod]
		public void Surface_CannotBeModified()
		{
			var expression = "(function (api) { try { api.escapeXml = null; api.extra = 1; api.curl.build = null; } catch (error) { } return [typeof api.escapeXml, typeof api.extra, typeof api.curl.build]; }(window.SoapUiWsdl))";

			var kinds = Strings(SoapUiJavaScriptHost.Evaluate(expression));

			CollectionAssert.AreEqual(new[] { "function", "undefined", "function" }, kinds);
		}

		// ------------------------------------------------------------------
		// Interpreter
		// ------------------------------------------------------------------
		// Several checks in the script anchor a pattern with $. A browser matches $ only at the very end of the text, and so
		// must the interpreter. If this test fails, the trailing line break rows below fail for the interpreter's sake, not the script's.
		[TestMethod]
		public void Interpreter_DollarAnchorDoesNotMatchBeforeAFinalLineBreak()
		{
			Assert.IsTrue(SoapUiJavaScriptHost.Evaluate("/^[a-z]*$/.test('a')").GetBoolean());
			Assert.IsFalse(SoapUiJavaScriptHost.Evaluate("/^[a-z]*$/.test('a\\n')").GetBoolean());
		}

		// ------------------------------------------------------------------
		// escapeXml
		// ------------------------------------------------------------------
		[TestMethod]
		public void EscapeXml_EscapesAllFiveCharacters()
		{
			var escaped = Call("escapeXml", Lit("&<>\"'")).GetString();

			Assert.AreEqual("&amp;&lt;&gt;&quot;&#39;", escaped);
		}

		[TestMethod]
		[DataRow("a&b", "a&amp;b")]
		[DataRow("<a>", "&lt;a&gt;")]
		[DataRow("\"x\"", "&quot;x&quot;")]
		[DataRow("it's", "it&#39;s")]
		[DataRow("<&>", "&lt;&amp;&gt;")]
		[DataRow("&amp;", "&amp;amp;")]
		[DataRow("&lt;", "&amp;lt;")]
		[DataRow("<a href=\"x\">it's & that</a>", "&lt;a href=&quot;x&quot;&gt;it&#39;s &amp; that&lt;/a&gt;")]
		[DataRow("plain text 123", "plain text 123")]
		public void EscapeXml_EscapesEachCharacterInOnePass(string text, string expected)
		{
			Assert.AreEqual(expected, Call("escapeXml", Lit(text)).GetString());
		}

		[TestMethod]
		public void EscapeXml_LeavesEmptyAndNonAsciiTextAlone()
		{
			Assert.AreEqual(string.Empty, Call("escapeXml", "''").GetString());
			Assert.AreEqual("caf\u00e9 \u65e5\u672c", Call("escapeXml", "'caf\\u00e9 \\u65e5\\u672c'").GetString());
		}

		[TestMethod]
		[DataRow("5", "5")]
		[DataRow("1.5", "1.5")]
		[DataRow("true", "true")]
		[DataRow("false", "false")]
		public void EscapeXml_ConvertsNonStringsToText(string valueSource, string expected)
		{
			Assert.AreEqual(expected, Call("escapeXml", valueSource).GetString());
		}

		// ------------------------------------------------------------------
		// hasDoctype
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("<!DOCTYPE html>", true)]
		[DataRow("<!doctype foo>", true)]
		[DataRow("<!DocType soap>", true)]
		[DataRow("<?xml version=\"1.0\"?><!DOCTYPE a><a/>", true)]
		[DataRow("<!ENTITY xxe SYSTEM \"file:///etc/passwd\">", true)]
		[DataRow("<!entity x 'y'>", true)]
		[DataRow("<a/>", false)]
		[DataRow("<?xml version=\"1.0\"?><a/>", false)]
		[DataRow("<soap:Envelope><soap:Body>DOCTYPE ENTITY</soap:Body></soap:Envelope>", false)]
		[DataRow("<a>&lt;!DOCTYPE</a>", false)]
		public void HasDoctype_DetectsDoctypeAndEntityDeclarationsIgnoringCase(string text, bool expected)
		{
			Assert.AreEqual(expected, Call("hasDoctype", Lit(text)).GetBoolean());
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		public void HasDoctype_EmptyOrNonStringInput_IsFalse(string valueSource)
		{
			Assert.IsFalse(Call("hasDoctype", valueSource).GetBoolean());
		}

		// ------------------------------------------------------------------
		// envelopeNamespace
		// ------------------------------------------------------------------
		[TestMethod]
		public void EnvelopeNamespace_ReturnsTheSoap11Namespace()
		{
			Assert.AreEqual("http://schemas.xmlsoap.org/soap/envelope/", Call("envelopeNamespace", "'1.1'").GetString());
		}

		[TestMethod]
		public void EnvelopeNamespace_ReturnsTheSoap12Namespace()
		{
			Assert.AreEqual("http://www.w3.org/2003/05/soap-envelope", Call("envelopeNamespace", "'1.2'").GetString());
		}

		[TestMethod]
		[DataRow("'2.0'")]
		[DataRow("''")]
		[DataRow("'1'")]
		[DataRow("'1.1 '")]
		[DataRow("'soap11'")]
		[DataRow("1.1")]
		[DataRow("1.2")]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("{}")]
		public void EnvelopeNamespace_UnsupportedVersion_ReturnsNull(string versionSource)
		{
			Assert.AreEqual(JsonValueKind.Null, Call("envelopeNamespace", versionSource).ValueKind);
		}

		// ------------------------------------------------------------------
		// protocolHeaders
		// ------------------------------------------------------------------
		[TestMethod]
		public void ProtocolHeaders_Soap11_SetsContentTypeAndQuotedSoapAction()
		{
			var headers = Call("protocolHeaders", "'1.1'", Lit(SoapAction));

			Assert.AreEqual(2, headers.GetArrayLength());
			AssertHeader(headers[0], "Content-Type", "text/xml; charset=utf-8");
			AssertHeader(headers[1], "SOAPAction", "\"" + SoapAction + "\"");
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		public void ProtocolHeaders_Soap11_EmptyAction_SendsAnEmptyQuotedSoapAction(string actionSource)
		{
			var headers = Call("protocolHeaders", "'1.1'", actionSource);

			Assert.AreEqual(2, headers.GetArrayLength());
			AssertHeader(headers[1], "SOAPAction", "\"\"");
		}

		[TestMethod]
		public void ProtocolHeaders_Soap12_CarriesTheActionInTheContentType()
		{
			var headers = Call("protocolHeaders", "'1.2'", Lit(SoapAction));

			Assert.AreEqual(1, headers.GetArrayLength());
			AssertHeader(headers[0], "Content-Type", "application/soap+xml; charset=utf-8; action=\"" + SoapAction + "\"");
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("undefined")]
		[DataRow("null")]
		public void ProtocolHeaders_Soap12_EmptyAction_HasNoActionParameter(string actionSource)
		{
			var headers = Call("protocolHeaders", "'1.2'", actionSource);

			Assert.AreEqual(1, headers.GetArrayLength());
			AssertHeader(headers[0], "Content-Type", "application/soap+xml; charset=utf-8");
		}

		// ------------------------------------------------------------------
		// validateAction
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("http://tempuri.org/ISampleService/Ping")]
		[DataRow("urn:Service#Operation")]
		[DataRow("a b")]
		[DataRow("it's")]
		[DataRow("!#$%&'()*+,-./:<=>?@[]^_`{|}~")]
		public void ValidateAction_AcceptsPrintableAscii(string action)
		{
			AssertOk(Call("validateAction", Lit(action)));
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		public void ValidateAction_EmptyOrNonStringAction_IsAccepted(string actionSource)
		{
			AssertOk(Call("validateAction", actionSource));
		}

		[TestMethod]
		[DataRow("'a\"b'")]
		[DataRow("'a\\\\b'")]
		[DataRow("'a;b'")]
		[DataRow("'text/xml; charset=utf-8'")]
		[DataRow("'caf\\u00e9'")]
		[DataRow("'\\u65e5\\u672c'")]
		[DataRow("'a\\tb'")]
		[DataRow("'a\\nb'")]
		[DataRow("'a\\n'")]
		[DataRow("'a\\r\\nb'")]
		[DataRow("'a\\u0000b'")]
		[DataRow("'a\\u001fb'")]
		[DataRow("'a\\u007fb'")]
		[DataRow("'\\u0085'")]
		[DataRow("'\\u2028'")]
		public void ValidateAction_RejectsQuoteBackslashSemicolonAndNonPrintableCharacters(string actionSource)
		{
			AssertFailed(Call("validateAction", actionSource));
		}

		// ------------------------------------------------------------------
		// validateHeader
		// ------------------------------------------------------------------
		[TestMethod]
		public void ValidateHeader_ValidHeader_ReturnsNameAndValue()
		{
			var result = Call("validateHeader", Lit("X-Trace-Id"), Lit("abc"));

			AssertOk(result);
			Assert.AreEqual("X-Trace-Id", result.GetProperty("name").GetString());
			Assert.AreEqual("abc", result.GetProperty("value").GetString());
		}

		[TestMethod]
		public void ValidateHeader_TrimsTheName()
		{
			var result = Call("validateHeader", Lit("  X-Trace-Id  "), Lit("abc"));

			AssertOk(result);
			Assert.AreEqual("X-Trace-Id", result.GetProperty("name").GetString());
		}

		[TestMethod]
		[DataRow("X")]
		[DataRow("X-Trace-Id")]
		[DataRow("X_Custom_Header_2")]
		[DataRow("x-lower-case")]
		public void ValidateHeader_AcceptsLettersDigitsHyphenAndUnderscore(string name)
		{
			AssertOk(Call("validateHeader", Lit(name), Lit("v")));
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("'   '")]
		[DataRow("'\\t\\n'")]
		[DataRow("null")]
		[DataRow("undefined")]
		public void ValidateHeader_RequiresAName(string nameSource)
		{
			AssertFailed(Call("validateHeader", nameSource, "'v'"));
		}

		[TestMethod]
		[DataRow("'Bad Name'")]
		[DataRow("'Bad:Name'")]
		[DataRow("'a.b'")]
		[DataRow("'a/b'")]
		[DataRow("'X-A!'")]
		[DataRow("'a(b)'")]
		[DataRow("'caf\\u00e9'")]
		[DataRow("'X-A\\nB'")]
		[DataRow("'X-A\\r\\nInjected: 1'")]
		[DataRow("'X-A\\u0000'")]
		public void ValidateHeader_RejectsNamesWithOtherCharacters(string nameSource)
		{
			AssertFailed(Call("validateHeader", nameSource, "'v'"));
		}

		[TestMethod]
		public void ValidateHeader_NameLength_AllowsAtMost128Characters()
		{
			AssertOk(Call("validateHeader", Lit(new string('a', 128)), Lit("v")));
			AssertFailed(Call("validateHeader", Lit(new string('a', 129)), Lit("v")));
		}

		[TestMethod]
		[DataRow("Content-Type")]
		[DataRow("content-type")]
		[DataRow("CONTENT-TYPE")]
		[DataRow("SOAPAction")]
		[DataRow("soapaction")]
		[DataRow("  SoapAction  ")]
		public void ValidateHeader_RejectsTheHeadersTheExplorerSetsItself(string name)
		{
			AssertFailed(Call("validateHeader", Lit(name), Lit("v")));
		}

		[TestMethod]
		[DataRow("Host")]
		[DataRow("host")]
		[DataRow("Content-Length")]
		[DataRow("Connection")]
		[DataRow("Cookie")]
		[DataRow("COOKIE")]
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
		public void ValidateHeader_RejectsForbiddenHeaderNames(string name)
		{
			AssertFailed(Call("validateHeader", Lit(name), Lit("v")));
		}

		[TestMethod]
		[DataRow("Proxy-Authorization")]
		[DataRow("proxy-foo")]
		[DataRow("PROXY-CONNECTION")]
		[DataRow("Sec-Fetch-Mode")]
		[DataRow("sec-ch-ua")]
		[DataRow("Sec-Anything")]
		[DataRow("Access-Control-Request-Method")]
		[DataRow("access-control-request-headers")]
		public void ValidateHeader_RejectsForbiddenHeaderPrefixes(string name)
		{
			AssertFailed(Call("validateHeader", Lit(name), Lit("v")));
		}

		[TestMethod]
		[DataRow("Accept")]
		[DataRow("User-Agent")]
		[DataRow("Access-Control-Allow-Origin")]
		[DataRow("Security")]
		[DataRow("Proxy")]
		[DataRow("X-Host")]
		[DataRow("X-Content-Type")]
		public void ValidateHeader_AcceptsNamesThatOnlyLookLikeForbiddenOnes(string name)
		{
			AssertOk(Call("validateHeader", Lit(name), Lit("v")));
		}

		[TestMethod]
		public void ValidateHeader_RejectsADuplicateName_IgnoringCase()
		{
			AssertFailed(Call("validateHeader", Lit("X-Trace-Id"), Lit("1"), "new Set(['x-trace-id'])"));
			AssertFailed(Call("validateHeader", Lit("x-TRACE-id"), Lit("1"), "new Set(['x-trace-id'])"));
		}

		[TestMethod]
		[DataRow("new Set()")]
		[DataRow("new Set(['x-other'])")]
		[DataRow("undefined")]
		[DataRow("null")]
		public void ValidateHeader_UsedNames_OnlyMatterForTheSameName(string usedNamesSource)
		{
			AssertOk(Call("validateHeader", Lit("X-Trace-Id"), Lit("1"), usedNamesSource));
		}

		[TestMethod]
		public void ValidateHeader_ReportsADistinctReasonForEachRule()
		{
			var usedNames = "new Set(['x-used'])";
			var errors = new[]
			{
				Call("validateHeader", "''", "'v'"),
				Call("validateHeader", "'Bad Name'", "'v'"),
				Call("validateHeader", "'Content-Type'", "'v'"),
				Call("validateHeader", "'Host'", "'v'"),
				Call("validateHeader", "'X-Used'", "'v'", usedNames),
				Call("validateHeader", "'X-A'", "'caf\\u00e9'"),
			};

			foreach (var error in errors)
			{
				AssertFailed(error);
			}

			var messages = errors.Select(error => error.GetProperty("error").GetString()).ToArray();
			Assert.AreEqual(messages.Length, messages.Distinct().Count(), "Every rule needs its own message: " + string.Join(" | ", messages));
		}

		[TestMethod]
		public void ValidateHeader_Value_AllowsUpTo4096Characters()
		{
			AssertOk(Call("validateHeader", Lit("X-A"), Lit(new string('a', 4096))));
			AssertFailed(Call("validateHeader", Lit("X-A"), Lit(new string('a', 4097))));
		}

		[TestMethod]
		public void ValidateHeader_Value_AcceptsEveryPrintableAsciiCharacter()
		{
			var printable = string.Concat(Enumerable.Range(0x20, 0x5F).Select(code => (char)code));

			var result = Call("validateHeader", Lit("X-A"), Lit(printable));

			AssertOk(result);
			Assert.AreEqual(printable, result.GetProperty("value").GetString());
		}

		[TestMethod]
		public void ValidateHeader_Value_AcceptsATab()
		{
			var result = Call("validateHeader", Lit("X-A"), "'a\\tb'");

			AssertOk(result);
			Assert.AreEqual("a\tb", result.GetProperty("value").GetString());
		}

		[TestMethod]
		[DataRow("'a\\nb'")]
		[DataRow("'a\\rb'")]
		[DataRow("'a\\n'")]
		[DataRow("'a\\r\\nInjected: 1'")]
		[DataRow("'a\\u0000b'")]
		[DataRow("'a\\u001fb'")]
		[DataRow("'a\\u007fb'")]
		[DataRow("'caf\\u00e9'")]
		[DataRow("'\\u2028'")]
		public void ValidateHeader_Value_RejectsNonPrintableAndNonAsciiCharacters(string valueSource)
		{
			AssertFailed(Call("validateHeader", Lit("X-A"), valueSource));
		}

		[TestMethod]
		public void ValidateHeader_EmptyValue_IsAcceptedAndMissingValuesBecomeEmpty()
		{
			foreach (var valueSource in new[] { "''", "null", "undefined" })
			{
				var result = Call("validateHeader", Lit("X-A"), valueSource);

				AssertOk(result);
				Assert.AreEqual(string.Empty, result.GetProperty("value").GetString(), "Value " + valueSource);
			}
		}

		[TestMethod]
		public void ValidateHeader_NumberValue_BecomesText()
		{
			var result = Call("validateHeader", Lit("X-A"), "5");

			AssertOk(result);
			Assert.AreEqual("5", result.GetProperty("value").GetString());
		}

		// ------------------------------------------------------------------
		// isSensitiveHeader
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("Authorization")]
		[DataRow("authorization")]
		[DataRow("AUTHORIZATION")]
		[DataRow("Proxy-Authorization")]
		[DataRow("Cookie")]
		[DataRow("Set-Cookie")]
		[DataRow("X-Api-Key")]
		[DataRow("X-Session-Id")]
		[DataRow("x-session-id")]
		[DataRow("X-CSRF-Token")]
		[DataRow("X-XSRF-Token")]
		[DataRow("X-Auth-Token")]
		[DataRow("X-Access-Token")]
		[DataRow("X-Secret")]
		[DataRow("X-Password")]
		[DataRow("X-Signature")]
		[DataRow("X-Jwt")]
		[DataRow("X-Bearer-Token")]
		[DataRow("X-Credentials")]
		public void IsSensitiveHeader_RecognisesCredentialHeaders(string name)
		{
			Assert.IsTrue(Call("isSensitiveHeader", Lit(name)).GetBoolean());
		}

		[TestMethod]
		[DataRow("X-Authorization")]
		[DataRow("X-Csrf-Header")]
		[DataRow("X-Custom-Authorization-Extra")]
		[DataRow("My-Session-Header")]
		public void IsSensitiveHeader_MatchesNamePartsAnywhereInTheName(string name)
		{
			Assert.IsTrue(Call("isSensitiveHeader", Lit(name)).GetBoolean());
		}

		[TestMethod]
		[DataRow("Accept")]
		[DataRow("Accept-Language")]
		[DataRow("Content-Type")]
		[DataRow("SOAPAction")]
		[DataRow("User-Agent")]
		[DataRow("Cache-Control")]
		[DataRow("X-Trace-Id")]
		[DataRow("X-Request-Id")]
		[DataRow("X-Correlation-Id")]
		[DataRow("X-Forwarded-For")]
		[DataRow("X-Tenant")]
		public void IsSensitiveHeader_OrdinaryHeaders_AreNotSensitive(string name)
		{
			Assert.IsFalse(Call("isSensitiveHeader", Lit(name)).GetBoolean());
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		[DataRow("{}")]
		public void IsSensitiveHeader_NonStringName_DoesNotThrow(string nameSource)
		{
			Assert.IsFalse(Call("isSensitiveHeader", nameSource).GetBoolean());
		}

		// ------------------------------------------------------------------
		// shellQuote
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("abc", "'abc'")]
		[DataRow("a b", "'a b'")]
		[DataRow("it's", "'it'\\''s'")]
		[DataRow("'", "''\\'''")]
		[DataRow("''", "''\\'''\\'''")]
		[DataRow("$(id)", "'$(id)'")]
		[DataRow("`id`", "'`id`'")]
		[DataRow("\"q\"", "'\"q\"'")]
		[DataRow("a\\b", "'a\\b'")]
		[DataRow("a;b&c|d>e", "'a;b&c|d>e'")]
		[DataRow("*?[~!#", "'*?[~!#'")]
		public void ShellQuote_WrapsInSingleQuotes_AndEscapesEmbeddedSingleQuotes(string text, string expected)
		{
			Assert.AreEqual(expected, Call("shellQuote", Lit(text)).GetString());
		}

		[TestMethod]
		public void ShellQuote_EmptyString_BecomesTwoQuotes()
		{
			Assert.AreEqual("''", Call("shellQuote", "''").GetString());
		}

		[TestMethod]
		public void ShellQuote_KeepsLineBreaksInsideTheQuotes()
		{
			Assert.AreEqual("'a\nb'", Call("shellQuote", "'a\\nb'").GetString());
		}

		[TestMethod]
		public void ShellQuote_ConvertsNonStringsToText()
		{
			Assert.AreEqual("'5'", Call("shellQuote", "5").GetString());
		}

		// ------------------------------------------------------------------
		// curl.build
		// ------------------------------------------------------------------
		[TestMethod]
		public void CurlBuild_BuildsAPostWithHeadersInOrderAndAHeredocBody()
		{
			var headers = new[] { "Content-Type", "text/xml; charset=utf-8", "SOAPAction", "\"" + SoapAction + "\"" };

			var result = BuildCurl(CurlUrl, "<a/>", headers);

			AssertOk(result);
			var expected = Lines(
				"curl -X POST 'https://example.com/Service.svc' \\",
				"  -H 'Content-Type: text/xml; charset=utf-8' \\",
				"  -H 'SOAPAction: \"http://tempuri.org/ISampleService/Ping\"' \\",
				"  --data-binary @- <<'SOAP_EOF'",
				"<a/>",
				"SOAP_EOF");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void CurlBuild_WithoutHeaders_BuildsJustTheCommandAndTheBody()
		{
			var result = BuildCurl(CurlUrl, "<a/>");

			AssertOk(result);
			var expected = Lines(
				"curl -X POST 'https://example.com/Service.svc' \\",
				"  --data-binary @- <<'SOAP_EOF'",
				"<a/>",
				"SOAP_EOF");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		public void CurlBuild_RedactsTheValuesOfSensitiveHeaders()
		{
			var headers = new[]
			{
				"Authorization", "Bearer top-secret-token",
				"Cookie", "session=cookie-secret",
				"X-Api-Key", "api-key-secret",
				"X-Session-Id", "session-id-secret",
				"X-CSRF-Token", "csrf-secret",
				"X-Trace-Id", "trace-visible"
			};

			var result = BuildCurl(CurlUrl, "<a/>", headers);

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			StringAssert.Contains(text, "-H 'Authorization: REDACTED'");
			StringAssert.Contains(text, "-H 'Cookie: REDACTED'");
			StringAssert.Contains(text, "-H 'X-Api-Key: REDACTED'");
			StringAssert.Contains(text, "-H 'X-Session-Id: REDACTED'");
			StringAssert.Contains(text, "-H 'X-CSRF-Token: REDACTED'");
			StringAssert.Contains(text, "-H 'X-Trace-Id: trace-visible'");
			foreach (var secret in new[] { "top-secret-token", "cookie-secret", "api-key-secret", "session-id-secret", "csrf-secret" })
			{
				Assert.IsFalse(text.Contains(secret), "The secret " + secret + " leaked into the command.");
			}
		}

		[TestMethod]
		public void CurlBuild_DoesNotRedactTheBody()
		{
			var result = BuildCurl(CurlUrl, "<password>hunter2</password>");

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "<password>hunter2</password>");
		}

		[TestMethod]
		public void CurlBuild_QuotesHeaderNamesAndValuesForTheShell()
		{
			var headers = new[] { "X-A", "it's", "X-Note", "$(id) `id` \"q\" ; & |" };

			var result = BuildCurl(CurlUrl, "x", headers);

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			StringAssert.Contains(text, "  -H 'X-A: it'\\''s' \\\n");
			StringAssert.Contains(text, "  -H 'X-Note: $(id) `id` \"q\" ; & |' \\\n");
		}

		[TestMethod]
		[DataRow("https://h/s.svc?a=b'c", "'https://h/s.svc?a=b'\\''c'")]
		[DataRow("https://h/$(id)", "'https://h/$(id)'")]
		[DataRow("https://h/`id`", "'https://h/`id`'")]
		[DataRow("https://h/s?a=1&b=2;c=3", "'https://h/s?a=1&b=2;c=3'")]
		[DataRow("https://h/a b", "'https://h/a b'")]
		[DataRow("https://h/\"x\"", "'https://h/\"x\"'")]
		public void CurlBuild_QuotesTheUrlForTheShell(string url, string quoted)
		{
			var result = BuildCurl(url, "x");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			var expectedStart = "curl -X POST " + quoted + " \\\n";
			Assert.IsTrue(text.StartsWith(expectedStart, StringComparison.Ordinal), "Expected the command to start with " + expectedStart + " but got " + text);
		}

		[TestMethod]
		public void CurlBuild_UsesAQuotedHeredocDelimiter_SoTheShellDoesNotExpandTheBody()
		{
			var result = BuildCurl(CurlUrl, "<a/>");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			AssertHeredoc(text, "SOAP_EOF");
			Assert.IsFalse(text.Contains("<<SOAP_EOF"), "The delimiter must be quoted.");
			Assert.IsFalse(text.Contains("<<\"SOAP_EOF\""), "The delimiter must be single quoted.");
		}

		[TestMethod]
		public void CurlBuild_ShellMetacharactersInTheBody_StayLiteral()
		{
			var body = "$(id) `id` \"q\" 's' \\ ${X} $HOME";

			var result = BuildCurl(CurlUrl, body);

			AssertOk(result);
			var expected = Lines(
				"curl -X POST 'https://example.com/Service.svc' \\",
				"  --data-binary @- <<'SOAP_EOF'",
				body,
				"SOAP_EOF");
			Assert.AreEqual(expected, result.GetProperty("text").GetString());
		}

		[TestMethod]
		[DataRow("'<a/>'", "SOAP_EOF")]
		[DataRow("'a\\nSOAP_EOF\\nb'", "SOAP_EOF_X")]
		[DataRow("'SOAP_EOF'", "SOAP_EOF_X")]
		[DataRow("'SOAP_EOF\\n'", "SOAP_EOF_X")]
		[DataRow("'x\\r\\nSOAP_EOF\\r\\ny'", "SOAP_EOF_X")]
		[DataRow("'SOAP_EOF\\rx'", "SOAP_EOF_X")]
		[DataRow("'SOAP_EOF\\nSOAP_EOF_X'", "SOAP_EOF_X_X")]
		[DataRow("'SOAP_EOF\\nSOAP_EOF_X\\nSOAP_EOF_X_X'", "SOAP_EOF_X_X_X")]
		[DataRow("' SOAP_EOF'", "SOAP_EOF")]
		[DataRow("'SOAP_EOF '", "SOAP_EOF")]
		[DataRow("'a SOAP_EOF b'", "SOAP_EOF")]
		[DataRow("'<a>SOAP_EOF</a>'", "SOAP_EOF")]
		[DataRow("'SOAP_EOF_X'", "SOAP_EOF")]
		[DataRow("'soap_eof'", "SOAP_EOF")]
		public void CurlBuild_PicksADelimiterThatIsNotALineOfTheBody(string bodySource, string expectedDelimiter)
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: [], body: " + bodySource + " }");

			AssertOk(result);
			AssertHeredoc(result.GetProperty("text").GetString(), expectedDelimiter);
		}

		[TestMethod]
		public void CurlBuild_NormalisesLineBreaksToLf()
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: [], body: 'a\\r\\nb\\rc\\nd' }");

			AssertOk(result);
			var text = result.GetProperty("text").GetString();
			Assert.IsFalse(text.Contains("\r"), "The command must not contain a carriage return.");
			var expected = Lines(
				"curl -X POST 'https://example.com/Service.svc' \\",
				"  --data-binary @- <<'SOAP_EOF'",
				"a",
				"b",
				"c",
				"d",
				"SOAP_EOF");
			Assert.AreEqual(expected, text);
		}

		[TestMethod]
		public void CurlBuild_TrailingNewlineOfTheBody_IsNotDoubled()
		{
			var plain = BuildCurl(CurlUrl, "<a/>").GetProperty("text").GetString();
			var terminated = BuildCurl(CurlUrl, "<a/>\n").GetProperty("text").GetString();
			var terminatedWithCrLf = BuildCurl(CurlUrl, "<a/>\r\n").GetProperty("text").GetString();

			Assert.AreEqual(plain, terminated);
			Assert.AreEqual(plain, terminatedWithCrLf);
		}

		[TestMethod]
		public void CurlBuild_BlankLinesInTheBody_ArePreserved()
		{
			var inside = BuildCurl(CurlUrl, "a\n\nb");
			var atTheEnd = BuildCurl(CurlUrl, "a\n\n");

			AssertOk(inside);
			AssertOk(atTheEnd);
			var expectedInside = Lines(
				"curl -X POST 'https://example.com/Service.svc' \\",
				"  --data-binary @- <<'SOAP_EOF'",
				"a",
				string.Empty,
				"b",
				"SOAP_EOF");
			var expectedAtTheEnd = Lines(
				"curl -X POST 'https://example.com/Service.svc' \\",
				"  --data-binary @- <<'SOAP_EOF'",
				"a",
				string.Empty,
				"SOAP_EOF");
			Assert.AreEqual(expectedInside, inside.GetProperty("text").GetString());
			Assert.AreEqual(expectedAtTheEnd, atTheEnd.GetProperty("text").GetString());
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("null")]
		[DataRow("undefined")]
		public void CurlBuild_EmptyBody_StillBuildsAWellFormedHeredoc(string bodySource)
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: [], body: " + bodySource + " }");

			AssertOk(result);
			AssertHeredoc(result.GetProperty("text").GetString(), "SOAP_EOF");
		}

		[TestMethod]
		public void CurlBuild_AcceptsTabCarriageReturnAndLineFeedInTheBody()
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: [], body: 'a\\tb\\r\\nc' }");

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "\na\tb\nc\nSOAP_EOF\n");
		}

		[TestMethod]
		[DataRow("'a\\u0000b'")]
		[DataRow("'a\\u0001b'")]
		[DataRow("'a\\u0008b'")]
		[DataRow("'a\\u000bb'")]
		[DataRow("'a\\u000cb'")]
		[DataRow("'a\\u001bb'")]
		[DataRow("'a\\u007fb'")]
		public void CurlBuild_RejectsOtherControlCharactersInTheBody(string bodySource)
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: [], body: " + bodySource + " }");

			AssertFailed(result);
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("'https://h/\\n'")]
		[DataRow("'https://h/\\r\\nHost: evil'")]
		[DataRow("'https://h/a\\tb'")]
		[DataRow("'https://h/\\u0000'")]
		[DataRow("'https://h/\\u001b[31m'")]
		[DataRow("'https://h/\\u007f'")]
		public void CurlBuild_RejectsAnEmptyUrlOrOneWithControlCharacters(string urlSource)
		{
			var result = Call("curl.build", "{ url: " + urlSource + ", headers: [], body: 'x' }");

			AssertFailed(result);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("{}")]
		[DataRow("{ headers: [], body: 'x' }")]
		public void CurlBuild_WithoutAUrl_Fails(string requestSource)
		{
			AssertFailed(Call("curl.build", requestSource));
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
		public void CurlBuild_RejectsHeadersWithAnEmptyNameOrControlCharacters(string nameSource, string valueSource)
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: [{ name: " + nameSource + ", value: " + valueSource + " }], body: 'x' }");

			AssertFailed(result);
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("{ value: 'x' }")]
		[DataRow("{ name: '', value: 'x' }")]
		[DataRow("{ name: undefined }")]
		public void CurlBuild_RejectsAHeaderWithoutAName(string headerSource)
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: [" + headerSource + "], body: 'x' }");

			AssertFailed(result);
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("'nope'")]
		[DataRow("{}")]
		[DataRow("5")]
		public void CurlBuild_IgnoresAHeaderListThatIsNotAnArray(string headersSource)
		{
			var result = Call("curl.build", "{ url: " + Lit(CurlUrl) + ", headers: " + headersSource + ", body: 'x' }");

			AssertOk(result);
			Assert.IsFalse(result.GetProperty("text").GetString().Contains(" -H "), "There must be no header argument.");
		}

		[TestMethod]
		public void CurlBuild_AcceptsEveryHeaderValueThatValidateHeaderAccepts()
		{
			var value = "a\tb";
			AssertOk(Call("validateHeader", Lit("X-Note"), Lit(value)));

			var result = BuildCurl(CurlUrl, "<a/>", "X-Note", value);

			AssertOk(result);
			StringAssert.Contains(result.GetProperty("text").GetString(), "-H 'X-Note: a\tb'");
		}

		// ------------------------------------------------------------------
		// shortenSoapAction
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("http://tempuri.org/ISampleService/Ping", "http://tempuri.org/", "ISampleService/Ping")]
		[DataRow("http://tempuri.org/ISampleService/Ping", "http://tempuri.org", "ISampleService/Ping")]
		[DataRow("http://tempuri.org/Ping", "http://tempuri.org/", "Ping")]
		[DataRow("http://tempuri.org//Ping", "http://tempuri.org/", "Ping")]
		[DataRow("urn:contract:ISampleService/Ping", "urn:contract:", "ISampleService/Ping")]
		public void ShortenSoapAction_StripsTheContractNamespace(string action, string contractNamespace, string expected)
		{
			Assert.AreEqual(expected, Call("shortenSoapAction", Lit(action), Lit(contractNamespace)).GetString());
		}

		[TestMethod]
		[DataRow("urn:other/Ping", "http://tempuri.org/")]
		[DataRow("http://other.org/Ping", "http://tempuri.org/")]
		[DataRow("http://tempuri.org/Ping", "http://tempuri.org/Other/")]
		[DataRow("Ping", "http://tempuri.org/")]
		[DataRow("http://tempuri.org/", "http://tempuri.org/")]
		[DataRow("http://tempuri.org", "http://tempuri.org/")]
		public void ShortenSoapAction_NamespaceIsNotAPrefixOrNothingWouldBeLeft_ReturnsTheActionUnchanged(string action, string contractNamespace)
		{
			Assert.AreEqual(action, Call("shortenSoapAction", Lit(action), Lit(contractNamespace)).GetString());
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("null")]
		[DataRow("undefined")]
		public void ShortenSoapAction_EmptyOrMissingNamespace_ReturnsTheActionUnchanged(string namespaceSource)
		{
			Assert.AreEqual(SoapAction, Call("shortenSoapAction", Lit(SoapAction), namespaceSource).GetString());
		}

		[TestMethod]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		[DataRow("{}")]
		public void ShortenSoapAction_NonStringAction_ReturnsAnEmptyString(string actionSource)
		{
			Assert.AreEqual(string.Empty, Call("shortenSoapAction", actionSource, Lit("http://tempuri.org/")).GetString());
		}

		// ------------------------------------------------------------------
		// formatBytes
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("0", "0 B")]
		[DataRow("1", "1 B")]
		[DataRow("512", "512 B")]
		[DataRow("1023", "1023 B")]
		[DataRow("1024", "1.0 KB")]
		[DataRow("1536", "1.5 KB")]
		[DataRow("10240", "10.0 KB")]
		[DataRow("1048576", "1.0 MB")]
		[DataRow("1572864", "1.5 MB")]
		[DataRow("5 * 1024 * 1024 * 1024", "5.0 GB")]
		public void FormatBytes_UsesBinaryStepsAndOneDecimalAboveBytes(string countSource, string expected)
		{
			Assert.AreEqual(expected, Call("formatBytes", countSource).GetString());
		}

		[TestMethod]
		[DataRow("-1")]
		[DataRow("-1024")]
		[DataRow("NaN")]
		[DataRow("Infinity")]
		[DataRow("-Infinity")]
		[DataRow("'1024'")]
		[DataRow("''")]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("true")]
		[DataRow("{}")]
		[DataRow("[]")]
		public void FormatBytes_InvalidInput_ReturnsZeroBytes(string countSource)
		{
			Assert.AreEqual("0 B", Call("formatBytes", countSource).GetString());
		}

		// ------------------------------------------------------------------
		// buildHashRoute
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("'service-svc'", "'Ping'", "#service-svc/Ping")]
		[DataRow("'my service'", "'Get Item'", "#my%20service/Get%20Item")]
		[DataRow("'a/b'", "'c/d'", "#a%2Fb/c%2Fd")]
		[DataRow("'100%'", "'50%'", "#100%25/50%25")]
		[DataRow("'a#b'", "'c?d'", "#a%23b/c%3Fd")]
		[DataRow("'caf\\u00e9'", "'\\u65e5\\u672c'", "#caf%C3%A9/%E6%97%A5%E6%9C%AC")]
		public void BuildHashRoute_EncodesServiceAndOperation(string serviceIdSource, string operationNameSource, string expected)
		{
			Assert.AreEqual(expected, Call("buildHashRoute", serviceIdSource, operationNameSource).GetString());
		}

		[TestMethod]
		[DataRow("'service-svc'", "''")]
		[DataRow("'service-svc'", "undefined")]
		[DataRow("'service-svc'", "null")]
		[DataRow("'service-svc'", "5")]
		[DataRow("'service-svc'", "{}")]
		public void BuildHashRoute_WithoutAnOperation_OnlyHasTheService(string serviceIdSource, string operationNameSource)
		{
			Assert.AreEqual("#service-svc", Call("buildHashRoute", serviceIdSource, operationNameSource).GetString());
		}

		[TestMethod]
		public void BuildHashRoute_OneArgument_OnlyHasTheService()
		{
			Assert.AreEqual("#my%20service", Call("buildHashRoute", "'my service'").GetString());
		}

		[TestMethod]
		[DataRow("undefined", "'Ping'")]
		[DataRow("null", "'Ping'")]
		[DataRow("5", "'Ping'")]
		[DataRow("{}", "'Ping'")]
		[DataRow("undefined", "undefined")]
		[DataRow("''", "undefined")]
		public void BuildHashRoute_MissingOrNonStringService_ReturnsABareHash(string serviceIdSource, string operationNameSource)
		{
			Assert.AreEqual("#", Call("buildHashRoute", serviceIdSource, operationNameSource).GetString());
		}

		// ------------------------------------------------------------------
		// parseHashRoute
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("#service-svc/Ping", "service-svc", "Ping")]
		[DataRow("service-svc/Ping", "service-svc", "Ping")]
		[DataRow("#my%20service/Get%20Item", "my service", "Get Item")]
		[DataRow("#a%2Fb/c%2Fd", "a/b", "c/d")]
		[DataRow("#100%25/50%25", "100%", "50%")]
		[DataRow("#a%23b/c%3Fd", "a#b", "c?d")]
		[DataRow("#a/b/c", "a", "b")]
		[DataRow("#a/b/", "a", "b")]
		[DataRow("#a/b/c/d/e", "a", "b")]
		public void ParseHashRoute_SplitsAndDecodesServiceAndOperation(string hash, string expectedServiceId, string expectedOperationName)
		{
			var route = Call("parseHashRoute", Lit(hash));

			AssertRoute(route, expectedServiceId, expectedOperationName);
		}

		[TestMethod]
		[DataRow("#service-svc")]
		[DataRow("service-svc")]
		[DataRow("#service-svc/")]
		public void ParseHashRoute_WithoutAnOperation_HasAnEmptyOperationName(string hash)
		{
			var route = Call("parseHashRoute", Lit(hash));

			AssertRoute(route, "service-svc", string.Empty);
		}

		[TestMethod]
		public void ParseHashRoute_DecodesUnicode()
		{
			var route = Call("parseHashRoute", Lit("#caf%C3%A9/%E6%97%A5%E6%9C%AC"));

			AssertRoute(route, "caf\u00e9", "\u65e5\u672c");
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("'#'")]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("[]")]
		[DataRow("true")]
		public void ParseHashRoute_EmptyOrNonStringInput_ReturnsEmptyParts(string hashSource)
		{
			var route = Call("parseHashRoute", hashSource);

			AssertRoute(route, string.Empty, string.Empty);
		}

		[TestMethod]
		[DataRow("'#%E0%A4%A/Ping'")]
		[DataRow("'#ok/%'")]
		[DataRow("'#%/x'")]
		[DataRow("'#a/%E0%A4%A'")]
		[DataRow("'#%C0%80/x'")]
		[DataRow("'#%FF'")]
		public void ParseHashRoute_MalformedEncoding_ReturnsEmptyPartsAndDoesNotThrow(string hashSource)
		{
			var route = Call("parseHashRoute", hashSource);

			AssertRoute(route, string.Empty, string.Empty);
		}

		[TestMethod]
		[DataRow("service-svc", "Ping")]
		[DataRow("my service", "Get Item")]
		[DataRow("a/b", "c/d")]
		[DataRow("100%", "50%")]
		[DataRow("a%2Fb", "c%20d")]
		[DataRow("a#b", "c?d")]
		[DataRow("a&b=c", "d+e")]
		[DataRow("#start", "end#")]
		[DataRow("caf\u00e9", "\u65e5\u672c\u8a9e")]
		public void HashRoute_BuildThenParse_RoundTrips(string serviceId, string operationName)
		{
			var result = RoundTrip(serviceId, operationName);

			Assert.IsTrue(result.GetProperty("hash").GetString().StartsWith('#'));
			AssertRoute(result.GetProperty("parsed"), serviceId, operationName);
		}

		[TestMethod]
		[DataRow("service-svc")]
		[DataRow("my service")]
		[DataRow("a/b")]
		[DataRow("100%")]
		public void HashRoute_BuildThenParse_RoundTripsAServiceWithoutAnOperation(string serviceId)
		{
			var result = RoundTrip(serviceId, null);

			AssertRoute(result.GetProperty("parsed"), serviceId, string.Empty);
		}

		// ------------------------------------------------------------------
		// buildSvcutilCommand
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("http://localhost:5000/Service.svc?wsdl")]
		[DataRow("https://example.com/Service.svc?wsdl")]
		[DataRow("https://example.com:8443/a/b.asmx?WSDL")]
		[DataRow("http://[::1]:5000/Service.svc?wsdl")]
		[DataRow("http://user@host/x")]
		[DataRow("http://h/a%20b?x=1#frag")]
		[DataRow("http://h/a~b_c.d-e,f=g+h!i*j")]
		public void BuildSvcutilCommand_AcceptsHttpAndHttpsUrls(string url)
		{
			var result = Call("buildSvcutilCommand", Lit(url));

			AssertOk(result);
			Assert.AreEqual("dotnet-svcutil \"" + url + "\"", result.GetProperty("text").GetString());
		}

		[TestMethod]
		[DataRow("http://h/s?a=1&b=2")]
		[DataRow("http://h/&")]
		[DataRow("http://h/\"x")]
		[DataRow("http://h/x\"; rm -rf /")]
		[DataRow("http://h/`id`")]
		[DataRow("http://h/$(id)")]
		[DataRow("http://h/$HOME")]
		[DataRow("http://h/x;id")]
		[DataRow("http://h/x|id")]
		[DataRow("http://h/x id")]
		[DataRow("http://h/x'y")]
		[DataRow("http://h/x(y)")]
		[DataRow("http://h/x<y>")]
		[DataRow("http://h/x\\y")]
		[DataRow("http://h/x{y}")]
		[DataRow("http://h/x^y")]
		public void BuildSvcutilCommand_RejectsCharactersThatAreSpecialToAShell(string url)
		{
			AssertFailed(Call("buildSvcutilCommand", Lit(url)));
		}

		[TestMethod]
		[DataRow("javascript:alert(1)")]
		[DataRow("file:///etc/passwd")]
		[DataRow("ftp://h/x")]
		[DataRow("data:text/plain,x")]
		[DataRow("//h/x")]
		[DataRow("/Service.svc?wsdl")]
		[DataRow("Service.svc?wsdl")]
		[DataRow("http:/h/x")]
		[DataRow("http://")]
		[DataRow("https://")]
		public void BuildSvcutilCommand_RejectsEverythingButAbsoluteHttpUrls(string url)
		{
			AssertFailed(Call("buildSvcutilCommand", Lit(url)));
		}

		[TestMethod]
		[DataRow("'http://h/x\\ny'")]
		[DataRow("'http://h/x\\n'")]
		[DataRow("'http://h/x\\r\\ny'")]
		[DataRow("'http://h/x\\ty'")]
		[DataRow("'http://h/x\\u0000y'")]
		[DataRow("'http://h/\\u00e9'")]
		[DataRow("' http://h/x'")]
		[DataRow("'http://h/x '")]
		public void BuildSvcutilCommand_RejectsControlCharactersWhitespaceAndNonAscii(string urlSource)
		{
			AssertFailed(Call("buildSvcutilCommand", urlSource));
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("undefined")]
		[DataRow("null")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("['http://h/x']")]
		public void BuildSvcutilCommand_EmptyOrNonStringInput_Fails(string urlSource)
		{
			AssertFailed(Call("buildSvcutilCommand", urlSource));
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

		// Joins the lines with LF and ends the text with a line break, the way curl.build ends its command.
		private static string Lines(params string[] lines)
		{
			return string.Join("\n", lines) + "\n";
		}

		// headerPairs holds name and value pairs: name1, value1, name2, value2.
		private static JsonElement BuildCurl(string url, string body, params string[] headerPairs)
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

			var request = "{ url: " + Lit(url) + ", headers: [" + string.Join(", ", headers) + "], body: " + Lit(body) + " }";
			return Call("curl.build", request);
		}

		private static JsonElement RoundTrip(string serviceId, string operationName)
		{
			var expression = "(function (id, operation) { var hash = window.SoapUiWsdl.buildHashRoute(id, operation); return { hash: hash, parsed: window.SoapUiWsdl.parseHashRoute(hash) }; }(" + Lit(serviceId) + ", " + Lit(operationName) + "))";
			return SoapUiJavaScriptHost.Evaluate(expression);
		}

		private static void AssertOk(JsonElement result)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected an object but got: " + result);
			Assert.IsTrue(result.GetProperty("ok").GetBoolean(), "Expected ok but got: " + result);
		}

		private static void AssertFailed(JsonElement result)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected an object but got: " + result);
			Assert.IsFalse(result.GetProperty("ok").GetBoolean(), "Expected a failure but got: " + result);
			Assert.AreEqual(JsonValueKind.String, result.GetProperty("error").ValueKind, "A failure needs an error message: " + result);
			Assert.IsFalse(string.IsNullOrWhiteSpace(result.GetProperty("error").GetString()), "A failure needs an error message: " + result);
			Assert.IsFalse(result.TryGetProperty("text", out _), "A failure must not carry any text: " + result);
		}

		private static void AssertHeader(JsonElement header, string name, string value)
		{
			Assert.AreEqual(name, header.GetProperty("name").GetString());
			Assert.AreEqual(value, header.GetProperty("value").GetString());
		}

		private static void AssertRoute(JsonElement route, string serviceId, string operationName)
		{
			Assert.AreEqual(JsonValueKind.String, route.GetProperty("serviceId").ValueKind, "serviceId must be a string: " + route);
			Assert.AreEqual(JsonValueKind.String, route.GetProperty("operationName").ValueKind, "operationName must be a string: " + route);
			Assert.AreEqual(serviceId, route.GetProperty("serviceId").GetString());
			Assert.AreEqual(operationName, route.GetProperty("operationName").GetString());
		}

		// The command must end with the delimiter on its own line, open the here-document with the quoted delimiter
		// and contain the delimiter as a whole line only once, as the terminator.
		private static void AssertHeredoc(string text, string delimiter)
		{
			Assert.IsTrue(text.EndsWith("\n" + delimiter + "\n", StringComparison.Ordinal), "The command must end with the delimiter on its own line: " + text);
			StringAssert.Contains(text, "  --data-binary @- <<'" + delimiter + "'\n");
			Assert.AreEqual(1, text.Split('\n').Count(line => line == delimiter), "The delimiter must be a whole line only once, as the terminator: " + text);
		}
	}
}
