using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.Ui
{
	// Unit tests of the Phase 3 additions to the explorer's page independent JavaScript (wsdl-parser.js), run in the Jint
	// interpreter: the typed values of the form editor (inputKindForType, validateXsdValue, sampleValueForType), the XML tokenizer
	// (tokenizeXml), the text search (findMatches), the form template (buildFormTemplate) and the WSDL outline (buildWsdlOutline).
	// The last two read the schema through the DOM, which Jint does not have. Those tests install a small XML parser that offers
	// the few DOM members the script uses (DOMParser, namespaceURI, localName, getAttribute, lookupNamespaceURI, firstElementChild
	// and nextElementSibling). Its results were compared with the ones of a real browser for the WSDL files of both serializers.
	// Test data written as JavaScript source (parameters named ...Source) keeps control characters out of the test names.
	[TestClass]
	public class SoapUiJavaScriptPhase3Tests
	{
		private const string Phase3Functions = "['buildFormTemplate', 'inputKindForType', 'validateXsdValue', 'sampleValueForType', 'tokenizeXml', 'findMatches', 'buildWsdlOutline']";

		private const string DefaultNamespace = "urn:t";

		private const string SoapEnvelope = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><a:Op xmlns:a='urn:t' id=\"1\">x</a:Op></soapenv:Body></soapenv:Envelope>";

		private const string ColorType = "<xs:simpleType name='Color'><xs:restriction base='xs:string'><xs:enumeration value='Red'/><xs:enumeration value='Green'/><xs:enumeration value='Blue'/></xs:restriction></xs:simpleType>";

		private const string OutlineWsdl = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' xmlns:soap='http://schemas.xmlsoap.org/wsdl/soap/' xmlns:soap12='http://schemas.xmlsoap.org/wsdl/soap12/' xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:tns='urn:t' targetNamespace='urn:t'>"
			+ "<wsdl:types><xs:schema targetNamespace='urn:t'>"
			+ "<xs:element name='Get'/><xs:element name='GetResponse'/><xs:element name='Fault'/>"
			+ "<xs:complexType name='Item'/><xs:complexType name='ArrayOfItem'/>"
			+ "<xs:simpleType name='Color'><xs:restriction base='xs:string'><xs:enumeration value='Red'/><xs:enumeration value='Green'/><xs:enumeration value='Blue'/></xs:restriction></xs:simpleType>"
			+ "<xs:simpleType name='Flags'><xs:list><xs:simpleType><xs:restriction base='xs:string'><xs:enumeration value='a'/></xs:restriction></xs:simpleType></xs:list></xs:simpleType>"
			+ "<xs:simpleType name='Code'><xs:restriction base='xs:string'><xs:pattern value='[A-Z]'/></xs:restriction></xs:simpleType>"
			+ "</xs:schema></wsdl:types>"
			+ "<wsdl:message name='GetIn'><wsdl:part name='parameters' element='tns:Get'/></wsdl:message>"
			+ "<wsdl:message name='GetOut'><wsdl:part name='parameters' element='tns:GetResponse'/></wsdl:message>"
			+ "<wsdl:message name='RpcIn'><wsdl:part name='a' type='xs:string'/><wsdl:part name='b' type='tns:Item'/></wsdl:message>"
			+ "<wsdl:message name='Fault1'><wsdl:part name='detail' element='tns:Fault'/></wsdl:message>"
			+ "<wsdl:portType name='P'>"
			+ "<wsdl:operation name='Get'><wsdl:input message='tns:GetIn'/><wsdl:output message='tns:GetOut'/><wsdl:fault name='f1' message='tns:Fault1'/><wsdl:fault name='Fault2'/></wsdl:operation>"
			+ "<wsdl:operation name='Set'><wsdl:input message='tns:SetIn'/></wsdl:operation>"
			+ "</wsdl:portType>"
			+ "<wsdl:binding name='B11' type='tns:P'><soap:binding transport='http://schemas.xmlsoap.org/soap/http' style='document'/><wsdl:operation name='Get'><soap:operation soapAction='urn:t/Get'/></wsdl:operation><wsdl:operation name='Set'><soap:operation soapAction='urn:t/Set'/></wsdl:operation></wsdl:binding>"
			+ "<wsdl:binding name='B12' type='tns:P'><soap12:binding transport='http://schemas.xmlsoap.org/soap/http'/><wsdl:operation name='Get'><soap12:operation soapAction='urn:t/Get'/></wsdl:operation></wsdl:binding>"
			+ "<wsdl:binding name='BHttp' type='tns:P'/>"
			+ "<wsdl:service name='S'>"
			+ "<wsdl:port name='P11' binding='tns:B11'><soap:address location='https://example.com/S.svc'/></wsdl:port>"
			+ "<wsdl:port name='P12' binding='tns:B12'><soap12:address location='https://example.com/S.svc'/></wsdl:port>"
			+ "<wsdl:port name='PNoAddress' binding='tns:B12'/>"
			+ "<wsdl:port name='PUnknown' binding='tns:Missing'/>"
			+ "</wsdl:service></wsdl:definitions>";

		// A small XML parser that offers what the script reads of the DOM. It is installed as DOMParser inside the expression a test evaluates.
		private const string MiniDom = @"
var scope = globalThis;
var DQ = String.fromCharCode(34);
var SQ = String.fromCharCode(39);
var own = Object.prototype.hasOwnProperty;

function decodeEntities(text) {
	return text.replace(/&(#x[0-9A-Fa-f]+|#[0-9]+|lt|gt|amp|quot|apos);/g, function (match, entity) {
		if (entity === 'lt') { return '<'; }
		if (entity === 'gt') { return '>'; }
		if (entity === 'amp') { return '&'; }
		if (entity === 'quot') { return DQ; }
		if (entity === 'apos') { return SQ; }
		return String.fromCharCode(entity.charAt(1) === 'x' ? parseInt(entity.slice(2), 16) : parseInt(entity.slice(1), 10));
	});
}

function MiniElement(qualifiedName, attrs, parent) {
	var colon = qualifiedName.indexOf(':');
	this.prefix = colon >= 0 ? qualifiedName.slice(0, colon) : null;
	this.localName = colon >= 0 ? qualifiedName.slice(colon + 1) : qualifiedName;
	this.tagName = qualifiedName;
	this.attrs = attrs;
	this.parent = parent;
	this.children = [];
	this.firstElementChild = null;
	this.nextElementSibling = null;
	this.namespaceURI = null;
}

MiniElement.prototype.getAttribute = function (name) {
	return own.call(this.attrs, name) ? this.attrs[name] : null;
};

MiniElement.prototype.lookupNamespaceURI = function (prefix) {
	var key = prefix === null || prefix === undefined ? 'xmlns' : 'xmlns:' + prefix;
	for (var node = this; node; node = node.parent) {
		if (own.call(node.attrs, key)) {
			return node.attrs[key] === '' ? null : node.attrs[key];
		}
	}

	return prefix === 'xml' ? 'http://www.w3.org/XML/1998/namespace' : null;
};

function parseAttributes(source) {
	var attrs = {};
	var index = 0;
	while (index < source.length) {
		while (/\s/.test(source.charAt(index))) { index += 1; }
		if (index >= source.length) { break; }
		var start = index;
		while (index < source.length && source.charAt(index) !== '=' && !/\s/.test(source.charAt(index))) { index += 1; }
		var name = source.slice(start, index);
		while (/\s/.test(source.charAt(index))) { index += 1; }
		if (source.charAt(index) !== '=') { throw new Error('attribute without a value: ' + name); }
		index += 1;
		while (/\s/.test(source.charAt(index))) { index += 1; }
		var quote = source.charAt(index);
		if (quote !== DQ && quote !== SQ) { throw new Error('attribute value without quotes: ' + name); }
		var end = source.indexOf(quote, index + 1);
		if (end < 0) { throw new Error('unterminated attribute value: ' + name); }
		attrs[name] = decodeEntities(source.slice(index + 1, end));
		index = end + 1;
	}

	return attrs;
}

function parseDocument(text) {
	var root = null;
	var current = null;
	var position = 0;
	while (position < text.length) {
		var open = text.indexOf('<', position);
		if (open < 0) { break; }
		if (text.startsWith('<?', open)) {
			var pi = text.indexOf('?>', open);
			if (pi < 0) { throw new Error('unterminated processing instruction'); }
			position = pi + 2;
		} else if (text.startsWith('<!--', open)) {
			var comment = text.indexOf('-->', open);
			if (comment < 0) { throw new Error('unterminated comment'); }
			position = comment + 3;
		} else if (text.startsWith('</', open)) {
			var close = text.indexOf('>', open);
			if (close < 0 || current === null || current.tagName !== text.slice(open + 2, close).trim()) { throw new Error('mismatched end tag'); }
			current = current.parent;
			position = close + 1;
		} else {
			var index = open + 1;
			var quote = '';
			while (index < text.length) {
				var character = text.charAt(index);
				if (quote !== '') {
					if (character === quote) { quote = ''; }
				} else if (character === DQ || character === SQ) {
					quote = character;
				} else if (character === '>') {
					break;
				}

				index += 1;
			}

			if (index >= text.length) { throw new Error('unterminated tag'); }
			var inner = text.slice(open + 1, index);
			var selfClosing = inner.charAt(inner.length - 1) === '/';
			if (selfClosing) { inner = inner.slice(0, -1); }
			var nameEnd = inner.search(/\s/);
			var qualifiedName = nameEnd < 0 ? inner : inner.slice(0, nameEnd);
			var element = new MiniElement(qualifiedName, parseAttributes(nameEnd < 0 ? '' : inner.slice(nameEnd)), current);
			element.namespaceURI = element.lookupNamespaceURI(element.prefix);
			if (current !== null) {
				var previous = current.children[current.children.length - 1];
				if (previous) { previous.nextElementSibling = element; } else { current.firstElementChild = element; }
				current.children.push(element);
			} else if (root !== null) {
				throw new Error('more than one root element');
			} else {
				root = element;
			}

			if (!selfClosing) { current = element; }
			position = index + 1;
		}
	}

	if (root === null || current !== null) { throw new Error('the document is not well-formed'); }
	return { documentElement: root, getElementsByTagNameNS: function () { return []; } };
}

function DOMParser() {}
DOMParser.prototype.parseFromString = function (text) { return parseDocument(text); };
scope.DOMParser = DOMParser;
";

		private static readonly string[] BuiltInTypes =
		{
			"string", "boolean", "decimal", "float", "double", "int", "integer", "long", "short", "byte", "unsignedInt", "unsignedLong",
			"unsignedShort", "unsignedByte", "positiveInteger", "nonNegativeInteger", "negativeInteger", "nonPositiveInteger", "dateTime",
			"date", "time", "duration", "base64Binary", "hexBinary", "anyURI", "guid", "char",
		};

		private static readonly string[] TokenTypes = { "decl", "comment", "cdata", "doctype", "punct", "tag", "attr", "string", "text" };

		// ------------------------------------------------------------------
		// Surface
		// ------------------------------------------------------------------
		[TestMethod]
		public void Surface_ExposesTheFunctionsOfPhase3()
		{
			var expression = "(function (names) { return names.filter(function (name) { return typeof window.SoapUiWsdl[name] !== 'function'; }); }(" + Phase3Functions + "))";

			var missing = Strings(SoapUiJavaScriptHost.Evaluate(expression));

			Assert.AreEqual(0, missing.Length, "Missing functions on window.SoapUiWsdl: " + string.Join(", ", missing));
		}

		[TestMethod]
		public void Surface_Phase3ExportsCannotBeReplaced()
		{
			var expression = "(function (api) { try { api.buildFormTemplate = null; api.inputKindForType = null; api.validateXsdValue = null; api.sampleValueForType = null; api.tokenizeXml = null; api.findMatches = null; api.buildWsdlOutline = null; } catch (error) { } return " + Phase3Functions + ".map(function (name) { return typeof api[name]; }); }(window.SoapUiWsdl))";

			var kinds = Strings(SoapUiJavaScriptHost.Evaluate(expression));

			CollectionAssert.AreEqual(Enumerable.Repeat("function", 7).ToArray(), kinds);
		}

		[TestMethod]
		public void Surface_BuildWsdlOutlineWithoutADomFailsInsteadOfThrowing()
		{
			var outline = Call("buildWsdlOutline", Lit("<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/'/>"));

			AssertFailed(outline);
		}

		[TestMethod]
		public void Surface_BuildFormTemplateWithASchemaThatIsNotMadeOfDomNodesFailsInsteadOfThrowing()
		{
			var model = "{ operations: new Map([['Op', { name: 'Op', inputMessage: 'm' }]]), messages: new Map([['m', { name: 'm', parts: [{ name: 'p', element: { ns: 'urn:t', local: 'Op', key: '{urn:t}Op' }, type: null }] }]]), elements: new Map([['{urn:t}Op', { node: {}, schema: { ns: 'urn:t' } }]]) }";

			var template = Call("buildFormTemplate", model, "{ name: 'Op' }", "'1.1'");

			AssertFailed(template);
			StringAssert.StartsWith(template.GetProperty("error").GetString(), "The form could not be generated");
		}

		// ------------------------------------------------------------------
		// inputKindForType
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("int", "integer", "numeric", "0")]
		[DataRow("long", "integer", "numeric", "0")]
		[DataRow("short", "integer", "numeric", "0")]
		[DataRow("byte", "integer", "numeric", "0")]
		[DataRow("unsignedInt", "integer", "numeric", "0")]
		[DataRow("unsignedLong", "integer", "numeric", "0")]
		[DataRow("unsignedShort", "integer", "numeric", "0")]
		[DataRow("unsignedByte", "integer", "numeric", "0")]
		[DataRow("integer", "integer", "numeric", "0")]
		[DataRow("positiveInteger", "integer", "numeric", "0")]
		[DataRow("nonNegativeInteger", "integer", "numeric", "0")]
		[DataRow("negativeInteger", "integer", "numeric", "0")]
		[DataRow("nonPositiveInteger", "integer", "numeric", "0")]
		[DataRow("decimal", "number", "decimal", "0.0")]
		[DataRow("float", "number", "decimal", "0.0")]
		[DataRow("double", "number", "decimal", "0.0")]
		[DataRow("boolean", "boolean", "text", "")]
		[DataRow("dateTime", "datetime", "text", "2025-01-01T12:00:00Z")]
		[DataRow("date", "date", "text", "2025-01-01")]
		[DataRow("time", "time", "text", "12:00:00")]
		[DataRow("duration", "duration", "text", "PT1H")]
		[DataRow("base64Binary", "base64", "text", "AAAA")]
		[DataRow("guid", "guid", "text", "00000000-0000-0000-0000-000000000000")]
		[DataRow("hexBinary", "text", "text", "")]
		[DataRow("anyURI", "text", "text", "")]
		[DataRow("char", "text", "text", "")]
		[DataRow("string", "text", "text", "")]
		[DataRow("token", "text", "text", "")]
		[DataRow("unknownType", "text", "text", "")]
		[DataRow("__proto__", "text", "text", "")]
		[DataRow("constructor", "text", "text", "")]
		[DataRow("", "text", "text", "")]
		public void InputKindForType_MapsABuiltInTypeToItsKind(string xsd, string kind, string inputMode, string placeholder)
		{
			var result = Call("inputKindForType", TypeSource(xsd));

			Assert.AreEqual(kind, result.GetProperty("kind").GetString());
			Assert.AreEqual(inputMode, result.GetProperty("inputMode").GetString());
			Assert.AreEqual(placeholder, result.GetProperty("placeholder").GetString());
			Assert.AreEqual(JsonValueKind.Null, result.GetProperty("pattern").ValueKind);
		}

		[TestMethod]
		[DataRow("string")]
		[DataRow("int")]
		[DataRow("boolean")]
		[DataRow("dateTime")]
		[DataRow("guid")]
		[DataRow("unknownType")]
		public void InputKindForType_AnEnumerationWinsOverTheType(string xsd)
		{
			var result = Call("inputKindForType", TypeSource(xsd, "['A', 'B']"));

			Assert.AreEqual("enum", result.GetProperty("kind").GetString());
			Assert.AreEqual("text", result.GetProperty("inputMode").GetString());
			Assert.AreEqual(string.Empty, result.GetProperty("placeholder").GetString());
		}

		[TestMethod]
		[DataRow("[]", "integer")]
		[DataRow("null", "integer")]
		[DataRow("'x'", "integer")]
		[DataRow("{}", "integer")]
		[DataRow("undefined", "integer")]
		public void InputKindForType_AnEmptyOrMalformedEnumerationIsIgnored(string enumSource, string kind)
		{
			var result = Call("inputKindForType", "{ xsd: 'int', enumValues: " + enumSource + ", pattern: null }");

			Assert.AreEqual(kind, result.GetProperty("kind").GetString());
		}

		[TestMethod]
		[DataRow("string", "[a-z]+")]
		[DataRow("int", "[0-9]{3}")]
		[DataRow("dateTime", "\\d{4}.*")]
		[DataRow("guid", "x")]
		public void InputKindForType_EchoesThePatternAsAHint(string xsd, string pattern)
		{
			var result = Call("inputKindForType", TypeSource(xsd, "null", Lit(pattern)));

			Assert.AreEqual(pattern, result.GetProperty("pattern").GetString());
		}

		[TestMethod]
		public void InputKindForType_EchoesThePatternOfAnEnumerationToo()
		{
			var result = Call("inputKindForType", TypeSource("string", "['A']", Lit("A")));

			Assert.AreEqual("enum", result.GetProperty("kind").GetString());
			Assert.AreEqual("A", result.GetProperty("pattern").GetString());
		}

		[TestMethod]
		[DataRow("5")]
		[DataRow("true")]
		[DataRow("{}")]
		public void InputKindForType_APatternThatIsNotTextGivesNoPattern(string patternSource)
		{
			var result = Call("inputKindForType", "{ xsd: 'string', enumValues: null, pattern: " + patternSource + " }");

			Assert.AreEqual(JsonValueKind.Null, result.GetProperty("pattern").ValueKind);
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("5")]
		[DataRow("'int'")]
		[DataRow("true")]
		[DataRow("[]")]
		[DataRow("['int']")]
		[DataRow("{}")]
		[DataRow("{ xsd: 5 }")]
		[DataRow("{ xsd: null }")]
		[DataRow("{ xsd: ['int'] }")]
		[DataRow("{ enumValues: null }")]
		[DataRow("{ get xsd() { throw new Error('boom'); } }")]
		[DataRow("{ get enumValues() { throw new Error('boom'); }, xsd: 'int' }")]
		public void InputKindForType_AnythingThatIsNotATypeInfoGivesText(string typeSource)
		{
			var result = Call("inputKindForType", typeSource);

			Assert.AreEqual("text", result.GetProperty("kind").GetString());
			Assert.AreEqual("text", result.GetProperty("inputMode").GetString());
			Assert.AreEqual(string.Empty, result.GetProperty("placeholder").GetString());
			Assert.AreEqual(JsonValueKind.Null, result.GetProperty("pattern").ValueKind);
		}

		[TestMethod]
		public void InputKindForType_ResultHasExactlyTheDocumentedProperties()
		{
			var names = SoapUiJavaScriptHost.Evaluate("Object.keys(window.SoapUiWsdl.inputKindForType({ xsd: 'int', enumValues: null, pattern: null })).sort()");

			CollectionAssert.AreEqual(new[] { "inputMode", "kind", "pattern", "placeholder" }, Strings(names));
		}

		// ------------------------------------------------------------------
		// sampleValueForType
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("string", "string")]
		[DataRow("boolean", "true")]
		[DataRow("int", "1")]
		[DataRow("integer", "1")]
		[DataRow("long", "1")]
		[DataRow("short", "1")]
		[DataRow("byte", "1")]
		[DataRow("unsignedInt", "1")]
		[DataRow("unsignedLong", "1")]
		[DataRow("unsignedShort", "1")]
		[DataRow("unsignedByte", "1")]
		[DataRow("positiveInteger", "1")]
		[DataRow("nonNegativeInteger", "0")]
		[DataRow("nonPositiveInteger", "0")]
		[DataRow("negativeInteger", "-1")]
		[DataRow("decimal", "1.5")]
		[DataRow("float", "1.5")]
		[DataRow("double", "1.5")]
		[DataRow("dateTime", "2025-01-01T12:00:00Z")]
		[DataRow("date", "2025-01-01")]
		[DataRow("time", "12:00:00")]
		[DataRow("duration", "PT1H")]
		[DataRow("base64Binary", "AAAA")]
		[DataRow("hexBinary", "00")]
		[DataRow("anyURI", "https://example.com")]
		[DataRow("guid", "00000000-0000-0000-0000-000000000000")]
		[DataRow("char", "65")]
		[DataRow("token", "string")]
		[DataRow("somethingElse", "string")]
		[DataRow("__proto__", "string")]
		[DataRow("", "string")]
		public void SampleValueForType_IsTheSampleOfTheBuiltInType(string xsd, string expected)
		{
			var sample = Call("sampleValueForType", TypeSource(xsd));

			Assert.AreEqual(expected, sample.GetString());
		}

		[TestMethod]
		public void SampleValueForType_IsTheFirstEnumerationValue()
		{
			Assert.AreEqual("Green", Call("sampleValueForType", TypeSource("string", "['Green', 'Red']")).GetString());
			Assert.AreEqual("7", Call("sampleValueForType", TypeSource("int", "['7', '8']")).GetString());
			Assert.AreEqual(string.Empty, Call("sampleValueForType", TypeSource("string", "['']")).GetString());
		}

		[TestMethod]
		public void SampleValueForType_TurnsAnEnumerationValueThatIsNotTextIntoText()
		{
			Assert.AreEqual("5", Call("sampleValueForType", "{ xsd: 'int', enumValues: [5, 6], pattern: null }").GetString());
		}

		[TestMethod]
		[DataRow("[]")]
		[DataRow("null")]
		[DataRow("'x'")]
		public void SampleValueForType_AnEmptyOrMalformedEnumerationFallsBackToTheType(string enumSource)
		{
			var sample = Call("sampleValueForType", "{ xsd: 'int', enumValues: " + enumSource + ", pattern: null }");

			Assert.AreEqual("1", sample.GetString());
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("5")]
		[DataRow("'int'")]
		[DataRow("[]")]
		[DataRow("{}")]
		[DataRow("{ xsd: 5 }")]
		[DataRow("{ xsd: null }")]
		[DataRow("{ get xsd() { throw new Error('boom'); } }")]
		public void SampleValueForType_AnythingThatIsNotATypeInfoGivesString(string typeSource)
		{
			Assert.AreEqual("string", Call("sampleValueForType", typeSource).GetString());
		}

		[TestMethod]
		public void SampleValueForType_IsValidForItsType()
		{
			foreach (var xsd in BuiltInTypes)
			{
				var sample = Call("sampleValueForType", TypeSource(xsd)).GetString();

				AssertValid(Call("validateXsdValue", TypeSource(xsd), Lit(sample)), xsd + " sample " + sample);
			}
		}

		[TestMethod]
		public void SampleValueForType_AgreesWithTheSampleOfTheEnvelopeBuilder()
		{
			// The envelope builder writes these samples for the same built-in types (builtinSample), so a form and a generated envelope agree.
			var expression = "(function (types) { return types.map(function (xsd) { return window.SoapUiWsdl.sampleValueForType({ xsd: xsd, enumValues: null, pattern: null }); }); }(['int', 'dateTime', 'boolean', 'decimal', 'date']))";

			var samples = Strings(SoapUiJavaScriptHost.Evaluate(expression));

			CollectionAssert.AreEqual(new[] { "1", "2025-01-01T12:00:00Z", "true", "1.5", "2025-01-01" }, samples);
		}

		// ------------------------------------------------------------------
		// validateXsdValue: valid and invalid values of each type
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("int", "0")]
		[DataRow("int", "42")]
		[DataRow("int", "2147483647")]
		[DataRow("int", "-2147483648")]
		[DataRow("int", "+5")]
		[DataRow("int", "007")]
		[DataRow("int", "-0")]
		[DataRow("long", "9223372036854775807")]
		[DataRow("long", "-9223372036854775808")]
		[DataRow("long", "0000000000000000000000000000001")]
		[DataRow("short", "32767")]
		[DataRow("short", "-32768")]
		[DataRow("byte", "127")]
		[DataRow("byte", "-128")]
		[DataRow("unsignedByte", "0")]
		[DataRow("unsignedByte", "255")]
		[DataRow("unsignedByte", "-0")]
		[DataRow("unsignedShort", "65535")]
		[DataRow("unsignedInt", "4294967295")]
		[DataRow("unsignedLong", "18446744073709551615")]
		[DataRow("positiveInteger", "1")]
		[DataRow("positiveInteger", "+1")]
		[DataRow("positiveInteger", "99999999999999999999999999999999")]
		[DataRow("nonNegativeInteger", "0")]
		[DataRow("nonNegativeInteger", "-0")]
		[DataRow("nonNegativeInteger", "12345678901234567890123456789012345678901234567890")]
		[DataRow("negativeInteger", "-1")]
		[DataRow("negativeInteger", "-99999999999999999999999999999999")]
		[DataRow("nonPositiveInteger", "0")]
		[DataRow("nonPositiveInteger", "-0")]
		[DataRow("nonPositiveInteger", "-99999999999999999999999")]
		[DataRow("integer", "0")]
		[DataRow("integer", "123456789012345678901234567890")]
		[DataRow("integer", "-123456789012345678901234567890")]
		[DataRow("decimal", "1.5")]
		[DataRow("decimal", ".5")]
		[DataRow("decimal", "5.")]
		[DataRow("decimal", "+1")]
		[DataRow("decimal", "-0.0")]
		[DataRow("decimal", "007.50")]
		[DataRow("float", "1e5")]
		[DataRow("float", "1.5E-3")]
		[DataRow("float", "INF")]
		[DataRow("float", "-INF")]
		[DataRow("float", "NaN")]
		[DataRow("double", ".5e3")]
		[DataRow("double", "-1.5E+10")]
		[DataRow("double", "3")]
		[DataRow("double", "INF")]
		[DataRow("double", "NaN")]
		[DataRow("boolean", "true")]
		[DataRow("boolean", "false")]
		[DataRow("boolean", "1")]
		[DataRow("boolean", "0")]
		[DataRow("dateTime", "2025-01-01T12:00:00Z")]
		[DataRow("dateTime", "2025-01-01T12:00:00")]
		[DataRow("dateTime", "2025-01-01T12:00:00.123")]
		[DataRow("dateTime", "2025-01-01T12:00:00.123456789Z")]
		[DataRow("dateTime", "2025-01-01T12:00:00+05:30")]
		[DataRow("dateTime", "2025-01-01T12:00:00-14:00")]
		[DataRow("dateTime", "2025-12-31T23:59:59Z")]
		[DataRow("dateTime", "0001-01-01T00:00:00Z")]
		[DataRow("dateTime", "9999-12-31T23:59:59.9999999Z")]
		[DataRow("dateTime", "12345-01-01T00:00:00Z")]
		[DataRow("date", "2025-01-01")]
		[DataRow("date", "2025-01-01Z")]
		[DataRow("date", "2025-01-01+02:00")]
		[DataRow("date", "2025-12-31")]
		[DataRow("time", "12:00:00")]
		[DataRow("time", "00:00:00")]
		[DataRow("time", "23:59:59")]
		[DataRow("time", "12:00:00.5Z")]
		[DataRow("time", "12:00:00-05:00")]
		[DataRow("duration", "PT1H")]
		[DataRow("duration", "P1Y2M3DT4H5M6S")]
		[DataRow("duration", "-P1D")]
		[DataRow("duration", "PT1.5S")]
		[DataRow("duration", "P1Y")]
		[DataRow("duration", "P1M")]
		[DataRow("duration", "P0D")]
		[DataRow("duration", "PT0S")]
		[DataRow("duration", "PT36H")]
		[DataRow("duration", "P1DT2H30M")]
		[DataRow("base64Binary", "")]
		[DataRow("base64Binary", "AAAA")]
		[DataRow("base64Binary", "AA==")]
		[DataRow("base64Binary", "AAA=")]
		[DataRow("base64Binary", "SGVsbG8gV29ybGQ=")]
		[DataRow("base64Binary", "AAAA AAAA")]
		[DataRow("hexBinary", "")]
		[DataRow("hexBinary", "00")]
		[DataRow("hexBinary", "0aFF")]
		[DataRow("hexBinary", "DEADBEEF")]
		[DataRow("guid", "00000000-0000-0000-0000-000000000000")]
		[DataRow("guid", "ABCDEF12-3456-7890-ABCD-EF1234567890")]
		[DataRow("guid", "abcdef12-3456-7890-abcd-ef1234567890")]
		[DataRow("guid", "AbCdEf12-3456-7890-aBcD-eF1234567890")]
		[DataRow("anyURI", "")]
		[DataRow("anyURI", "https://example.com/a?b=c#d")]
		[DataRow("anyURI", "urn:x:y")]
		[DataRow("anyURI", "../relative/path")]
		[DataRow("anyURI", "mailto:a@b.c")]
		[DataRow("char", "0")]
		[DataRow("char", "65")]
		[DataRow("char", "65535")]
		[DataRow("string", "")]
		[DataRow("string", "anything at all <&>")]
		[DataRow("string", "  padded  ")]
		[DataRow("token", "x y")]
		[DataRow("somethingElse", "anything")]
		[DataRow("__proto__", "anything")]
		public void ValidateXsdValue_AcceptsAValidValue(string xsd, string value)
		{
			AssertValid(Call("validateXsdValue", TypeSource(xsd), Lit(value)), xsd + " " + value);
		}

		[TestMethod]
		[DataRow("int", "")]
		[DataRow("int", "abc")]
		[DataRow("int", "1.5")]
		[DataRow("int", "1e3")]
		[DataRow("int", "--1")]
		[DataRow("int", "+-1")]
		[DataRow("int", "1 2")]
		[DataRow("int", "0x10")]
		[DataRow("int", "1,000")]
		[DataRow("int", "-")]
		[DataRow("int", "+")]
		[DataRow("int", "2147483648")]
		[DataRow("int", "-2147483649")]
		[DataRow("long", "9223372036854775808")]
		[DataRow("long", "-9223372036854775809")]
		[DataRow("short", "32768")]
		[DataRow("short", "-32769")]
		[DataRow("byte", "128")]
		[DataRow("byte", "-129")]
		[DataRow("unsignedByte", "256")]
		[DataRow("unsignedByte", "-1")]
		[DataRow("unsignedShort", "65536")]
		[DataRow("unsignedShort", "-1")]
		[DataRow("unsignedInt", "4294967296")]
		[DataRow("unsignedInt", "-1")]
		[DataRow("unsignedLong", "18446744073709551616")]
		[DataRow("unsignedLong", "-1")]
		[DataRow("positiveInteger", "0")]
		[DataRow("positiveInteger", "-0")]
		[DataRow("positiveInteger", "-5")]
		[DataRow("nonNegativeInteger", "-1")]
		[DataRow("negativeInteger", "0")]
		[DataRow("negativeInteger", "-0")]
		[DataRow("negativeInteger", "1")]
		[DataRow("nonPositiveInteger", "1")]
		[DataRow("nonPositiveInteger", "+1")]
		[DataRow("integer", "1e5")]
		[DataRow("integer", "1.0")]
		[DataRow("integer", "\u0663")]
		[DataRow("integer", "\uFF11\uFF12")]
		[DataRow("decimal", "")]
		[DataRow("decimal", ".")]
		[DataRow("decimal", "1e5")]
		[DataRow("decimal", "1,5")]
		[DataRow("decimal", "--1")]
		[DataRow("decimal", "1.2.3")]
		[DataRow("decimal", "NaN")]
		[DataRow("decimal", "INF")]
		[DataRow("float", "")]
		[DataRow("float", "1e")]
		[DataRow("float", "e5")]
		[DataRow("float", "+INF")]
		[DataRow("float", "nan")]
		[DataRow("float", "inf")]
		[DataRow("float", "Infinity")]
		[DataRow("float", "1,5")]
		[DataRow("double", "0x10")]
		[DataRow("double", "1_000")]
		[DataRow("double", "1e5e5")]
		[DataRow("boolean", "")]
		[DataRow("boolean", "TRUE")]
		[DataRow("boolean", "True")]
		[DataRow("boolean", "yes")]
		[DataRow("boolean", "2")]
		[DataRow("boolean", "-1")]
		[DataRow("boolean", "tru")]
		[DataRow("dateTime", "")]
		[DataRow("dateTime", "2025-01-01")]
		[DataRow("dateTime", "12:00:00")]
		[DataRow("dateTime", "2025-01-01 12:00:00")]
		[DataRow("dateTime", "2025-01-01T12:00Z")]
		[DataRow("dateTime", "2025-1-1T12:00:00")]
		[DataRow("dateTime", "2025-01-01T12:00:00z")]
		[DataRow("dateTime", "2025-01-01T12:00:00.")]
		[DataRow("dateTime", "2025-01-01T12:00:00+5:00")]
		[DataRow("dateTime", "2025-01-01T12:00:00+0500")]
		[DataRow("dateTime", "2025-13-01T00:00:00")]
		[DataRow("dateTime", "2025-00-10T00:00:00")]
		[DataRow("dateTime", "2025-01-32T00:00:00")]
		[DataRow("dateTime", "2025-01-00T00:00:00")]
		[DataRow("dateTime", "0000-01-01T00:00:00")]
		[DataRow("dateTime", "2025-01-01T25:00:00")]
		[DataRow("dateTime", "2025-01-01T12:60:00")]
		[DataRow("dateTime", "2025-01-01T12:00:60")]
		[DataRow("date", "")]
		[DataRow("date", "2025-01-01T00:00:00")]
		[DataRow("date", "20250101")]
		[DataRow("date", "25-01-01")]
		[DataRow("date", "2025-02-30")]
		[DataRow("date", "2025-00-10")]
		[DataRow("date", "2025-01-00")]
		[DataRow("date", "2023-02-29")]
		[DataRow("time", "")]
		[DataRow("time", "12:00")]
		[DataRow("time", "1:00:00")]
		[DataRow("time", "12:00:00.")]
		[DataRow("time", "T12:00:00")]
		[DataRow("time", "25:00:00")]
		[DataRow("time", "12:60:00")]
		[DataRow("time", "12:00:60")]
		[DataRow("duration", "")]
		[DataRow("duration", "P")]
		[DataRow("duration", "PT")]
		[DataRow("duration", "P1YT")]
		[DataRow("duration", "PT.5S")]
		[DataRow("duration", "P1.5D")]
		[DataRow("duration", "1D")]
		[DataRow("duration", "P1W")]
		[DataRow("duration", "P-1D")]
		[DataRow("duration", "PT1H1D")]
		[DataRow("duration", "P1D1Y")]
		[DataRow("duration", "pt1h")]
		[DataRow("base64Binary", "A")]
		[DataRow("base64Binary", "AAA")]
		[DataRow("base64Binary", "AAAAA")]
		[DataRow("base64Binary", "A===")]
		[DataRow("base64Binary", "====")]
		[DataRow("base64Binary", "=AAA")]
		[DataRow("base64Binary", "AA=A")]
		[DataRow("base64Binary", "AAAA=")]
		[DataRow("base64Binary", "AA*=")]
		[DataRow("base64Binary", "AA-_")]
		[DataRow("base64Binary", "AAAA AAA")]
		[DataRow("hexBinary", "0")]
		[DataRow("hexBinary", "000")]
		[DataRow("hexBinary", "0G")]
		[DataRow("hexBinary", "0x00")]
		[DataRow("hexBinary", "AA BB")]
		[DataRow("guid", "")]
		[DataRow("guid", "{00000000-0000-0000-0000-000000000000}")]
		[DataRow("guid", "00000000000000000000000000000000")]
		[DataRow("guid", "0000000-0000-0000-0000-000000000000")]
		[DataRow("guid", "00000000-0000-0000-0000-00000000000")]
		[DataRow("guid", "00000000-0000-0000-0000-0000000000000")]
		[DataRow("guid", "0000000g-0000-0000-0000-000000000000")]
		[DataRow("guid", "00000000-0000-0000 -0000-000000000000")]
		[DataRow("anyURI", "a b")]
		[DataRow("anyURI", "https://example.com/a b")]
		[DataRow("char", "")]
		[DataRow("char", "A")]
		[DataRow("char", "AB")]
		[DataRow("char", "-1")]
		[DataRow("char", "65536")]
		[DataRow("char", "1.5")]
		public void ValidateXsdValue_RejectsAnInvalidValue(string xsd, string value)
		{
			AssertInvalid(Call("validateXsdValue", TypeSource(xsd), Lit(value)), xsd + " " + value);
		}

		[TestMethod]
		[DataRow("anyURI", "'a\\tb'")]
		[DataRow("anyURI", "'a\\nb'")]
		[DataRow("anyURI", "'a\\u0000b'")]
		[DataRow("anyURI", "'a\\u0085b'")]
		[DataRow("anyURI", "'a\\u007fb'")]
		[DataRow("anyURI", "'a\\u00a0b'")]
		[DataRow("int", "'4\\u00a02'")]
		[DataRow("int", "'\\u00a042'")]
		[DataRow("int", "'42\\u00a0'")]
		[DataRow("int", "'42\\u2003'")]
		[DataRow("int", "'4\\t2'")]
		[DataRow("boolean", "'\\u00a0true'")]
		[DataRow("hexBinary", "'00\\n11'")]
		public void ValidateXsdValue_RejectsWhitespaceThatIsNotXmlWhitespaceOrIsInsideTheValue(string xsd, string valueSource)
		{
			AssertInvalid(Call("validateXsdValue", TypeSource(xsd), valueSource), xsd + " " + valueSource);
		}

		// ------------------------------------------------------------------
		// validateXsdValue: boundaries, calendar and number details
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("int", "-2147483648", "2147483647")]
		[DataRow("long", "-9223372036854775808", "9223372036854775807")]
		[DataRow("short", "-32768", "32767")]
		[DataRow("byte", "-128", "127")]
		[DataRow("unsignedByte", "0", "255")]
		[DataRow("unsignedShort", "0", "65535")]
		[DataRow("unsignedInt", "0", "4294967295")]
		[DataRow("unsignedLong", "0", "18446744073709551615")]
		public void ValidateXsdValue_TheLimitsOfTheIntegerTypesAreExact(string xsd, string minimum, string maximum)
		{
			var belowMinimum = minimum == "0" ? "-1" : "-" + IncrementDigits(minimum.TrimStart('-'));
			var aboveMaximum = IncrementDigits(maximum);

			AssertValid(Call("validateXsdValue", TypeSource(xsd), Lit(minimum)), xsd + " minimum");
			AssertValid(Call("validateXsdValue", TypeSource(xsd), Lit(maximum)), xsd + " maximum");
			AssertInvalid(Call("validateXsdValue", TypeSource(xsd), Lit(belowMinimum)), xsd + " below the minimum " + belowMinimum);
			AssertInvalid(Call("validateXsdValue", TypeSource(xsd), Lit(aboveMaximum)), xsd + " above the maximum " + aboveMaximum);
		}

		[TestMethod]
		public void ValidateXsdValue_AnOutOfRangeErrorNamesTheType()
		{
			var message = Call("validateXsdValue", TypeSource("int"), Lit("2147483648")).GetProperty("error").GetString();

			StringAssert.Contains(message, "xs:int");
			StringAssert.Contains(message, "-2147483648");
			StringAssert.Contains(message, "2147483647");
		}

		[TestMethod]
		public void ValidateXsdValue_LeadingZerosDoNotChangeTheValue()
		{
			AssertValid(Call("validateXsdValue", TypeSource("int"), Lit("0000000000002147483647")), "int with leading zeros at the maximum");
			AssertInvalid(Call("validateXsdValue", TypeSource("int"), Lit("0000000000002147483648")), "int with leading zeros above the maximum");
			AssertValid(Call("validateXsdValue", TypeSource("unsignedByte"), Lit("000255")), "unsignedByte with leading zeros");
		}

		[TestMethod]
		[DataRow("2024-02-29", true)]
		[DataRow("2000-02-29", true)]
		[DataRow("2400-02-29", true)]
		[DataRow("1996-02-29", true)]
		[DataRow("2025-02-29", false)]
		[DataRow("1900-02-29", false)]
		[DataRow("2100-02-29", false)]
		[DataRow("2023-02-29", false)]
		[DataRow("2025-02-28", true)]
		[DataRow("2025-02-30", false)]
		[DataRow("2025-01-31", true)]
		[DataRow("2025-03-31", true)]
		[DataRow("2025-04-30", true)]
		[DataRow("2025-04-31", false)]
		[DataRow("2025-06-31", false)]
		[DataRow("2025-09-31", false)]
		[DataRow("2025-11-31", false)]
		[DataRow("2025-12-31", true)]
		[DataRow("2025-12-32", false)]
		[DataRow("2025-13-01", false)]
		[DataRow("2025-00-01", false)]
		public void ValidateXsdValue_DatesFollowTheCalendarIncludingLeapYears(string date, bool valid)
		{
			var asDate = Call("validateXsdValue", TypeSource("date"), Lit(date));
			var asDateTime = Call("validateXsdValue", TypeSource("dateTime"), Lit(date + "T10:00:00"));

			Assert.AreEqual(valid, asDate.GetProperty("ok").GetBoolean(), "date " + date + ": " + asDate);
			Assert.AreEqual(valid, asDateTime.GetProperty("ok").GetBoolean(), "dateTime " + date + ": " + asDateTime);
		}

		[TestMethod]
		[DataRow("T24:00:00", true)]
		[DataRow("T24:00:00.000", true)]
		[DataRow("T24:00:00Z", true)]
		[DataRow("T24:00:00.1", false)]
		[DataRow("T24:00:01", false)]
		[DataRow("T24:01:00", false)]
		[DataRow("T24:00:60", false)]
		[DataRow("T25:00:00", false)]
		[DataRow("T23:59:59", true)]
		[DataRow("T23:59:60", false)]
		[DataRow("T23:60:00", false)]
		[DataRow("T00:00:00", true)]
		public void ValidateXsdValue_TwentyFourHoursIsOnlyTheEndOfTheDay(string timePart, bool valid)
		{
			var asDateTime = Call("validateXsdValue", TypeSource("dateTime"), Lit("2025-01-01" + timePart));
			var asTime = Call("validateXsdValue", TypeSource("time"), Lit(timePart.TrimStart('T')));

			Assert.AreEqual(valid, asDateTime.GetProperty("ok").GetBoolean(), "dateTime " + timePart + ": " + asDateTime);
			Assert.AreEqual(valid, asTime.GetProperty("ok").GetBoolean(), "time " + timePart + ": " + asTime);
		}

		[TestMethod]
		[DataRow("Z", true)]
		[DataRow("+00:00", true)]
		[DataRow("-00:00", true)]
		[DataRow("+05:30", true)]
		[DataRow("-05:30", true)]
		[DataRow("+13:59", true)]
		[DataRow("+14:00", true)]
		[DataRow("-14:00", true)]
		[DataRow("+14:01", false)]
		[DataRow("+14:30", false)]
		[DataRow("+15:00", false)]
		[DataRow("-15:00", false)]
		[DataRow("+99:00", false)]
		[DataRow("+05:60", false)]
		[DataRow("+05:99", false)]
		[DataRow("+5:00", false)]
		[DataRow("+0500", false)]
		[DataRow("+05", false)]
		[DataRow("05:00", false)]
		[DataRow("z", false)]
		[DataRow("UTC", false)]
		public void ValidateXsdValue_TimeZoneOffsetsAreLimitedToFourteenHours(string zone, bool valid)
		{
			foreach (var sample in new[] { "dateTime|2025-01-01T12:00:00", "time|12:00:00", "date|2025-01-01" })
			{
				var parts = sample.Split('|');
				var result = Call("validateXsdValue", TypeSource(parts[0]), Lit(parts[1] + zone));

				Assert.AreEqual(valid, result.GetProperty("ok").GetBoolean(), parts[0] + " with " + zone + ": " + result);
			}
		}

		[TestMethod]
		public void ValidateXsdValue_AFractionOfASecondCanHaveAnyNumberOfDigits()
		{
			AssertValid(Call("validateXsdValue", TypeSource("dateTime"), Lit("2025-01-01T12:00:00.1")), "one digit");
			AssertValid(Call("validateXsdValue", TypeSource("dateTime"), Lit("2025-01-01T12:00:00.123456789012345")), "fifteen digits");
			AssertValid(Call("validateXsdValue", TypeSource("time"), Lit("12:00:00.0000001+01:00")), "seven digits with an offset");
			AssertInvalid(Call("validateXsdValue", TypeSource("time"), Lit("12:00:00.")), "a point without digits");
			AssertInvalid(Call("validateXsdValue", TypeSource("time"), Lit("12:00.5")), "a fraction without seconds");
		}

		[TestMethod]
		public void ValidateXsdValue_YearsCanHaveMoreThanFourDigitsButNotBeZero()
		{
			AssertValid(Call("validateXsdValue", TypeSource("date"), Lit("10000-01-01")), "five digits");
			AssertValid(Call("validateXsdValue", TypeSource("date"), Lit("0001-01-01")), "year one");
			AssertInvalid(Call("validateXsdValue", TypeSource("date"), Lit("0000-01-01")), "year zero");
			AssertInvalid(Call("validateXsdValue", TypeSource("date"), Lit("000000-01-01")), "year zero with six digits");
			AssertInvalid(Call("validateXsdValue", TypeSource("date"), Lit("999-01-01")), "three digits");
		}

		[TestMethod]
		public void ValidateXsdValue_AFloatingPointNumberIsOnlyCheckedForItsForm()
		{
			AssertValid(Call("validateXsdValue", TypeSource("float"), Lit("1e999")), "an exponent beyond the range");
			AssertValid(Call("validateXsdValue", TypeSource("double"), Lit("-1E-999")), "an exponent below the range");
			AssertValid(Call("validateXsdValue", TypeSource("double"), Lit("1." + new string('0', 400))), "many digits");
			AssertInvalid(Call("validateXsdValue", TypeSource("decimal"), Lit("1e5")), "decimal has no exponent");
		}

		[TestMethod]
		public void ValidateXsdValue_HugeDigitStringsAreHandledWithoutPrecisionLoss()
		{
			var digits = new string('9', 10000);

			AssertValid(Call("validateXsdValue", TypeSource("integer"), Lit(digits)), "integer with 10000 digits");
			AssertValid(Call("validateXsdValue", TypeSource("integer"), Lit("-" + digits)), "negative integer with 10000 digits");
			AssertValid(Call("validateXsdValue", TypeSource("positiveInteger"), Lit(digits)), "positiveInteger");
			AssertValid(Call("validateXsdValue", TypeSource("nonNegativeInteger"), Lit(digits)), "nonNegativeInteger");
			AssertInvalid(Call("validateXsdValue", TypeSource("negativeInteger"), Lit(digits)), "negativeInteger");
			AssertInvalid(Call("validateXsdValue", TypeSource("nonPositiveInteger"), Lit(digits)), "nonPositiveInteger");
			AssertInvalid(Call("validateXsdValue", TypeSource("int"), Lit(digits)), "int");
			AssertInvalid(Call("validateXsdValue", TypeSource("long"), Lit(digits)), "long");
			AssertInvalid(Call("validateXsdValue", TypeSource("unsignedLong"), Lit(digits)), "unsignedLong");
			AssertValid(Call("validateXsdValue", TypeSource("decimal"), Lit(digits + "." + digits)), "decimal");
			AssertValid(Call("validateXsdValue", TypeSource("int"), Lit(new string('0', 10000) + "7")), "int with 10000 leading zeros");
		}

		[TestMethod]
		[DataRow("9223372036854775807", "9223372036854775806")]
		[DataRow("9007199254740993", "9007199254740992")]
		public void ValidateXsdValue_NumbersBeyondTheSafeIntegerRangeAreStillExact(string valueInRange, string alsoInRange)
		{
			// 2^53 + 1 is not a JavaScript number, so a comparison that used Number would get it wrong.
			AssertValid(Call("validateXsdValue", TypeSource("long"), Lit(valueInRange)), valueInRange);
			AssertValid(Call("validateXsdValue", TypeSource("long"), Lit(alsoInRange)), alsoInRange);
			AssertInvalid(Call("validateXsdValue", TypeSource("long"), Lit("9223372036854775808")), "just above long");
			AssertInvalid(Call("validateXsdValue", TypeSource("long"), Lit("9223372036854775809")), "just above long, not a double");
		}

		// ------------------------------------------------------------------
		// validateXsdValue: whitespace
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("int", "' 42 '")]
		[DataRow("int", "'\\t42\\r\\n'")]
		[DataRow("int", "'\\n\\n 42'")]
		[DataRow("long", "'  -9223372036854775808  '")]
		[DataRow("decimal", "' 1.5 '")]
		[DataRow("double", "'\\t1e5\\t'")]
		[DataRow("boolean", "' true '")]
		[DataRow("boolean", "'\\n0\\n'")]
		[DataRow("dateTime", "' 2025-01-01T12:00:00Z '")]
		[DataRow("date", "'\\t2025-01-01\\n'")]
		[DataRow("time", "' 12:00:00 '")]
		[DataRow("duration", "' PT1H '")]
		[DataRow("base64Binary", "' AAAA '")]
		[DataRow("base64Binary", "'AA\\r\\nAA\\tAAAA'")]
		[DataRow("hexBinary", "' 00 '")]
		[DataRow("guid", "' 00000000-0000-0000-0000-000000000000 '")]
		[DataRow("anyURI", "' https://example.com '")]
		[DataRow("char", "' 65 '")]
		[DataRow("string", "'  \\t kept \\n '")]
		[DataRow("string", "'\\u00a0'")]
		[DataRow("token", "'  x  '")]
		public void ValidateXsdValue_WhitespaceAroundAValueIsIgnoredExceptForStrings(string xsd, string valueSource)
		{
			AssertValid(Call("validateXsdValue", TypeSource(xsd), valueSource), xsd + " " + valueSource);
		}

		[TestMethod]
		public void ValidateXsdValue_AStringPatternSeesTheWhitespaceAndANumberPatternDoesNot()
		{
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "null", Lit("[a-z]+")), "' abc '"), "string with spaces");
			AssertValid(Call("validateXsdValue", TypeSource("string", "null", Lit(" ?[a-z]+ ?")), "' abc '"), "string with a pattern that allows the spaces");
			AssertValid(Call("validateXsdValue", TypeSource("int", "null", Lit("[0-9]{2}")), "' 12 '"), "int with spaces");
		}

		// ------------------------------------------------------------------
		// validateXsdValue: enumerations
		// ------------------------------------------------------------------
		[TestMethod]
		public void ValidateXsdValue_AnEnumerationAcceptsOnlyItsValues()
		{
			AssertValid(Call("validateXsdValue", TypeSource("string", "['Red', 'Green']"), Lit("Red")), "Red");
			AssertValid(Call("validateXsdValue", TypeSource("string", "['Red', 'Green']"), Lit("Green")), "Green");
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "['Red', 'Green']"), Lit("Blue")), "Blue");
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "['Red', 'Green']"), Lit("red")), "case matters");
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "['Red', 'Green']"), Lit(string.Empty)), "empty");
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "['Red', 'Green']"), Lit("Red Green")), "both");
		}

		[TestMethod]
		public void ValidateXsdValue_AStringEnumerationKeepsWhitespaceAndANumberEnumerationIgnoresIt()
		{
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "['Red']"), Lit(" Red")), "string with a space");
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "['Red']"), Lit("Red ")), "string with a trailing space");
			AssertValid(Call("validateXsdValue", TypeSource("string", "[' Red']"), Lit(" Red")), "string value that has a space");
			AssertValid(Call("validateXsdValue", TypeSource("int", "['1', '2']"), Lit(" 1 ")), "int with spaces");
			AssertInvalid(Call("validateXsdValue", TypeSource("int", "['1', '2']"), Lit("3")), "int that is not listed");
		}

		[TestMethod]
		public void ValidateXsdValue_AnEnumerationDecidesInsteadOfTheLexicalForm()
		{
			// A listed value is accepted even when it would not be valid for the type: the schema author listed it.
			AssertValid(Call("validateXsdValue", TypeSource("int", "['n/a']"), Lit("n/a")), "listed value of an int enumeration");
			AssertInvalid(Call("validateXsdValue", TypeSource("int", "['1']"), Lit("2")), "valid int that is not listed");
		}

		[TestMethod]
		public void ValidateXsdValue_AnEnumerationDecidesInsteadOfThePattern()
		{
			AssertValid(Call("validateXsdValue", TypeSource("string", "['abc']", Lit("[A-Z]+")), Lit("abc")), "listed value");
			AssertInvalid(Call("validateXsdValue", TypeSource("string", "['abc']", Lit("[a-z]+")), Lit("def")), "matching value that is not listed");
		}

		[TestMethod]
		[DataRow("[]")]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("'x'")]
		[DataRow("{}")]
		public void ValidateXsdValue_AnEmptyOrMalformedEnumerationIsIgnored(string enumSource)
		{
			AssertValid(Call("validateXsdValue", "{ xsd: 'int', enumValues: " + enumSource + ", pattern: null }", Lit("5")), "valid int");
			AssertInvalid(Call("validateXsdValue", "{ xsd: 'int', enumValues: " + enumSource + ", pattern: null }", Lit("x")), "invalid int");
		}

		[TestMethod]
		public void ValidateXsdValue_EnumerationValuesThatAreNotTextAreComparedAsText()
		{
			AssertValid(Call("validateXsdValue", "{ xsd: 'int', enumValues: [1, 2], pattern: null }", Lit("2")), "2");
			AssertInvalid(Call("validateXsdValue", "{ xsd: 'int', enumValues: [1, 2], pattern: null }", Lit("3")), "3");
		}

		// ------------------------------------------------------------------
		// validateXsdValue: patterns
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("[A-Z]{3}", "ABC", true)]
		[DataRow("[A-Z]{3}", "ABCD", false)]
		[DataRow("[A-Z]{3}", "AB", false)]
		[DataRow("[A-Z]{3}", "abc", false)]
		[DataRow("[A-Z]{3}", "", false)]
		[DataRow("[A-Z]{3}", "xABCx", false)]
		[DataRow("a|b", "a", true)]
		[DataRow("a|b", "b", true)]
		[DataRow("a|b", "ab", false)]
		[DataRow("a|b", "", false)]
		[DataRow("\\d{3}-\\d{4}", "555-1234", true)]
		[DataRow("\\d{3}-\\d{4}", "5555-1234", false)]
		[DataRow(".*", "anything", true)]
		[DataRow("[a-z]+@[a-z]+\\.com", "me@host.com", true)]
		[DataRow("[a-z]+@[a-z]+\\.com", "me@host.org", false)]
		[DataRow("", "", true)]
		[DataRow("", "x", false)]
		public void ValidateXsdValue_APatternMustMatchTheWholeValue(string pattern, string value, bool valid)
		{
			var result = Call("validateXsdValue", TypeSource("string", "null", Lit(pattern)), Lit(value));

			Assert.AreEqual(valid, result.GetProperty("ok").GetBoolean(), "pattern " + pattern + " value " + value + ": " + result);
		}

		[TestMethod]
		public void ValidateXsdValue_APatternMismatchSaysSo()
		{
			var result = Call("validateXsdValue", TypeSource("string", "null", Lit("[A-Z]{3}")), Lit("abc"));

			AssertInvalid(result, "mismatch");
			Assert.AreEqual("Does not match the pattern", result.GetProperty("error").GetString());
		}

		[TestMethod]
		[DataRow("[A-Z")]
		[DataRow("(")]
		[DataRow("(unclosed")]
		[DataRow("a{2,1}")]
		[DataRow("*abc")]
		[DataRow("[z-a]")]
		[DataRow("\\")]
		public void ValidateXsdValue_APatternThatDoesNotCompileIsIgnored(string pattern)
		{
			AssertValid(Call("validateXsdValue", TypeSource("string", "null", Lit(pattern)), Lit("anything")), "pattern " + pattern);
		}

		[TestMethod]
		[DataRow("\\i\\c*", "1abc")]
		[DataRow("\\c+", "!!!")]
		[DataRow("\\I+", "abc")]
		[DataRow("\\C+", "abc")]
		[DataRow("[a-z-[aeiou]]+", "aei")]
		[DataRow("\\p{IsBasicLatin}+", "\u00E9")]
		public void ValidateXsdValue_APatternWithXmlSchemaOnlyConstructsIsIgnored(string pattern, string value)
		{
			// JavaScript would read these differently, so the pattern is not used instead of rejecting good values.
			AssertValid(Call("validateXsdValue", TypeSource("string", "null", Lit(pattern)), Lit(value)), "pattern " + pattern);
		}

		[TestMethod]
		public void ValidateXsdValue_APatternWithAUnicodeCategoryIsUsedWhenTheEngineSupportsIt()
		{
			var accepted = Call("validateXsdValue", TypeSource("string", "null", Lit("\\p{L}+")), Lit("abc"));
			var rejected = Call("validateXsdValue", TypeSource("string", "null", Lit("\\p{L}+")), Lit("ab1"));

			AssertValid(accepted, "letters");
			AssertInvalid(rejected, "letters and a digit");
		}

		[TestMethod]
		[DataRow("5")]
		[DataRow("true")]
		[DataRow("{}")]
		[DataRow("['a']")]
		[DataRow("undefined")]
		public void ValidateXsdValue_APatternThatIsNotTextIsIgnored(string patternSource)
		{
			AssertValid(Call("validateXsdValue", "{ xsd: 'string', enumValues: null, pattern: " + patternSource + " }", Lit("anything")), "pattern " + patternSource);
		}

		[TestMethod]
		public void ValidateXsdValue_AFacetAddsToTheLexicalCheckOfTheType()
		{
			var typeSource = TypeSource("int", "null", Lit("[0-9]{3}"));

			AssertValid(Call("validateXsdValue", typeSource, Lit("123")), "three digits");
			AssertInvalid(Call("validateXsdValue", typeSource, Lit("12")), "two digits");
			var notANumber = Call("validateXsdValue", typeSource, Lit("abc"));
			AssertInvalid(notANumber, "not a number");
			Assert.AreNotEqual("Does not match the pattern", notANumber.GetProperty("error").GetString(), "The lexical error comes first.");
			AssertInvalid(Call("validateXsdValue", typeSource, Lit("2147483648")), "three digits are never out of range, but ten are");
		}

		[TestMethod]
		public void ValidateXsdValue_AValueLongerThanTheLimitIsNotRunThroughThePattern()
		{
			var result = Call("validateXsdValue", TypeSource("string", "null", Lit("a")), Lit(new string('b', 5001)));

			AssertValid(result, "a value that cannot be checked is not rejected");
		}

		[TestMethod]
		public void ValidateXsdValue_AValueAtTheLimitIsRunThroughThePattern()
		{
			var rejected = Call("validateXsdValue", TypeSource("string", "null", Lit("a*")), Lit(new string('b', 5000)));
			var accepted = Call("validateXsdValue", TypeSource("string", "null", Lit("a*")), Lit(new string('a', 5000)));

			AssertInvalid(rejected, "5000 characters that do not match");
			AssertValid(accepted, "5000 characters that match");
		}

		// ------------------------------------------------------------------
		// validateXsdValue: arguments that are not what they should be
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("5")]
		[DataRow("'int'")]
		[DataRow("true")]
		[DataRow("[]")]
		[DataRow("['int']")]
		[DataRow("{}")]
		[DataRow("{ xsd: 5 }")]
		[DataRow("{ xsd: null, enumValues: null, pattern: null }")]
		public void ValidateXsdValue_AnythingThatIsNotATypeInfoAcceptsEveryText(string typeSource)
		{
			AssertValid(Call("validateXsdValue", typeSource, Lit("anything <&> 123")), "type " + typeSource);
			AssertValid(Call("validateXsdValue", typeSource, Lit(string.Empty)), "empty text with type " + typeSource);
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("5")]
		[DataRow("true")]
		[DataRow("{}")]
		[DataRow("[]")]
		[DataRow("['a']")]
		public void ValidateXsdValue_ATextThatIsNotAStringFails(string textSource)
		{
			var withType = Call("validateXsdValue", TypeSource("string"), textSource);
			var withoutType = Call("validateXsdValue", "null", textSource);

			AssertInvalid(withType, "string type, text " + textSource);
			AssertInvalid(withoutType, "no type, text " + textSource);
		}

		[TestMethod]
		[DataRow("{ get xsd() { throw new Error('boom'); } }")]
		[DataRow("{ xsd: 'int', get enumValues() { throw new Error('boom'); } }")]
		[DataRow("{ xsd: 'string', enumValues: null, get pattern() { throw new Error('boom'); } }")]
		[DataRow("{ xsd: 'string', enumValues: [{ toString: function () { throw new Error('boom'); } }], pattern: null }")]
		public void ValidateXsdValue_NeverThrows(string typeSource)
		{
			var result = Call("validateXsdValue", typeSource, Lit("5"));

			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected a result object: " + result);
			Assert.IsTrue(result.TryGetProperty("ok", out _), "Expected an ok property: " + result);
		}

		[TestMethod]
		public void ValidateXsdValue_AFailureHasAShortMessageAndNothingElse()
		{
			var result = Call("validateXsdValue", TypeSource("int"), Lit("abc"));

			CollectionAssert.AreEquivalent(new[] { "ok", "error" }, result.EnumerateObject().Select(property => property.Name).ToArray());
			Assert.IsTrue(result.GetProperty("error").GetString().Length < 100, "The message should be a short sentence: " + result);
			Assert.IsTrue(result.GetProperty("error").GetString().EndsWith('.'), "The message should be a sentence: " + result);
		}

		[TestMethod]
		public void ValidateXsdValue_ASuccessHasOnlyOk()
		{
			var result = Call("validateXsdValue", TypeSource("int"), Lit("5"));

			CollectionAssert.AreEqual(new[] { "ok" }, result.EnumerateObject().Select(property => property.Name).ToArray());
		}

		[TestMethod]
		public void ValidateXsdValue_EveryTypeGivesADifferentMessageForADifferentProblem()
		{
			var messages = new[]
			{
				ErrorOf("int", "abc"),
				ErrorOf("int", "2147483648"),
				ErrorOf("positiveInteger", "0"),
				ErrorOf("decimal", "x"),
				ErrorOf("float", "x"),
				ErrorOf("boolean", "x"),
				ErrorOf("dateTime", "x"),
				ErrorOf("dateTime", "2025-02-30T00:00:00"),
				ErrorOf("dateTime", "2025-01-01T25:00:00"),
				ErrorOf("dateTime", "2025-01-01T00:00:00+15:00"),
				ErrorOf("date", "x"),
				ErrorOf("time", "x"),
				ErrorOf("duration", "x"),
				ErrorOf("base64Binary", "x"),
				ErrorOf("hexBinary", "x"),
				ErrorOf("guid", "x"),
				ErrorOf("anyURI", "a b"),
			};

			Assert.AreEqual(messages.Length, messages.Distinct().Count(), "The messages should tell the problems apart: " + string.Join(" | ", messages));
		}

		// ------------------------------------------------------------------
		// tokenizeXml: token types
		// ------------------------------------------------------------------
		[TestMethod]
		public void TokenizeXml_AnElementWithAnAttributeAndText()
		{
			var tokens = Tokens("<a:b x=\"1\" y='2'>t</a:b>");

			CollectionAssert.AreEqual(
				new[]
				{
					"punct|<", "tag|a:b", "text| ", "attr|x", "punct|=", "string|\"1\"", "text| ", "attr|y", "punct|=", "string|'2'", "punct|>",
					"text|t", "punct|</", "tag|a:b", "punct|>",
				},
				tokens);
		}

		[TestMethod]
		public void TokenizeXml_AnEmptyElementEndsWithASlashAndAngleBracket()
		{
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|br", "punct|/>" }, Tokens("<br/>"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|br", "text| ", "punct|/>" }, Tokens("<br />"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "attr|b", "punct|=", "string|\"x\"", "punct|/>", "text|t" }, Tokens("<a b=\"x\"/>t"));
		}

		[TestMethod]
		public void TokenizeXml_TheDeclarationIsOneToken()
		{
			CollectionAssert.AreEqual(
				new[] { "decl|<?xml version=\"1.0\" encoding=\"utf-8\"?>", "text|\n", "punct|<", "tag|r", "punct|/>" },
				Tokens("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<r/>"));
		}

		[TestMethod]
		public void TokenizeXml_AProcessingInstructionIsADeclarationToken()
		{
			CollectionAssert.AreEqual(new[] { "text|x", "decl|<?php echo 1 ?>", "text|y" }, Tokens("x<?php echo 1 ?>y"));
			CollectionAssert.AreEqual(new[] { "decl|<?xml-stylesheet href=\"a.xsl\"?>" }, Tokens("<?xml-stylesheet href=\"a.xsl\"?>"));
		}

		[TestMethod]
		public void TokenizeXml_ACommentIsOneTokenAndMayContainDashesAndMarkup()
		{
			CollectionAssert.AreEqual(new[] { "comment|<!-- a -- b -->", "punct|<", "tag|x", "punct|/>" }, Tokens("<!-- a -- b --><x/>"));
			CollectionAssert.AreEqual(new[] { "comment|<!-- <a href=\"x\">not a tag</a> -->" }, Tokens("<!-- <a href=\"x\">not a tag</a> -->"));
			CollectionAssert.AreEqual(new[] { "comment|<!---->" }, Tokens("<!---->"));
			CollectionAssert.AreEqual(new[] { "comment|<!-- - -->" }, Tokens("<!-- - -->"));
		}

		[TestMethod]
		public void TokenizeXml_ACdataSectionIsOneTokenAndMayContainClosingBrackets()
		{
			CollectionAssert.AreEqual(new[] { "cdata|<![CDATA[a ]] b ]> c]]>" }, Tokens("<![CDATA[a ]] b ]> c]]>"));
			CollectionAssert.AreEqual(new[] { "cdata|<![CDATA[]]>" }, Tokens("<![CDATA[]]>"));
			CollectionAssert.AreEqual(new[] { "cdata|<![CDATA[<a>&amp;</a>]]>", "punct|<", "tag|b", "punct|/>" }, Tokens("<![CDATA[<a>&amp;</a>]]><b/>"));
		}

		[TestMethod]
		public void TokenizeXml_ADoctypeIsOneTokenEvenWithAnInternalSubset()
		{
			CollectionAssert.AreEqual(new[] { "doctype|<!DOCTYPE html>" }, Tokens("<!DOCTYPE html>"));
			CollectionAssert.AreEqual(
				new[] { "doctype|<!DOCTYPE a [<!ENTITY e \"v>\">]>", "punct|<", "tag|a", "punct|/>" },
				Tokens("<!DOCTYPE a [<!ENTITY e \"v>\">]><a/>"));
			CollectionAssert.AreEqual(new[] { "doctype|<!DOCTYPE a SYSTEM \"a>b.dtd\">" }, Tokens("<!DOCTYPE a SYSTEM \"a>b.dtd\">"));
		}

		[TestMethod]
		public void TokenizeXml_AnyOtherMarkupDeclarationIsReadLikeADoctype()
		{
			CollectionAssert.AreEqual(new[] { "doctype|<!x>" }, Tokens("<!x>"));
			CollectionAssert.AreEqual(new[] { "doctype|<!ELEMENT a (b)>" }, Tokens("<!ELEMENT a (b)>"));
		}

		[TestMethod]
		public void TokenizeXml_AnAttributeValueKeepsItsQuotesAndMayContainAngleBracketsAndTheOtherQuote()
		{
			CollectionAssert.AreEqual(
				new[] { "punct|<", "tag|a", "text| ", "attr|href", "punct|=", "string|\"x>y\"", "text| ", "attr|t", "punct|=", "string|'a\"b<c'", "punct|>" },
				Tokens("<a href=\"x>y\" t='a\"b<c'>"));
		}

		[TestMethod]
		public void TokenizeXml_NamespacePrefixesStayPartOfTheNames()
		{
			CollectionAssert.AreEqual(
				new[]
				{
					"punct|<", "tag|ns:el", "text| ", "attr|xmlns:ns", "punct|=", "string|\"urn:x\"", "text| ", "attr|ns:attr", "punct|=", "string|\"v\"", "punct|/>",
				},
				Tokens("<ns:el xmlns:ns=\"urn:x\" ns:attr=\"v\"/>"));
		}

		[TestMethod]
		public void TokenizeXml_WhiteSpaceInsideATagIsText()
		{
			CollectionAssert.AreEqual(
				new[] { "punct|<", "tag|a", "text|\n\t", "attr|b", "punct|=", "string|\"1\"", "text|\r\n", "punct|/>" },
				Tokens("<a\n\tb=\"1\"\r\n/>"));
			CollectionAssert.AreEqual(
				new[] { "punct|<", "tag|a", "text| ", "attr|b", "text| ", "punct|=", "text| ", "string|\"1\"", "text| ", "punct|/", "text| ", "punct|>" },
				Tokens("<a b = \"1\" / >"));
		}

		[TestMethod]
		public void TokenizeXml_CharacterDataAndEntitiesBetweenTagsAreText()
		{
			CollectionAssert.AreEqual(
				new[] { "punct|<", "tag|a", "punct|>", "text|&amp;&lt; \u00E9 \uD83D\uDE00\n", "punct|</", "tag|a", "punct|>" },
				Tokens("<a>&amp;&lt; \u00E9 \uD83D\uDE00\n</a>"));
		}

		[TestMethod]
		public void TokenizeXml_AnEndTagHasATagNameAndPunctuation()
		{
			CollectionAssert.AreEqual(new[] { "punct|</", "tag|a", "punct|>" }, Tokens("</a>"));
			CollectionAssert.AreEqual(new[] { "punct|</", "tag|a", "text| ", "punct|>" }, Tokens("</a >"));
		}

		[TestMethod]
		public void TokenizeXml_ASoapEnvelopeGivesTheExpectedNamesInOrder()
		{
			var tags = SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.tokenizeXml(" + Lit(SoapEnvelope) + ").tokens.filter(function (token) { return token.type === 'tag'; }).map(function (token) { return token.text; })");
			var attributes = SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.tokenizeXml(" + Lit(SoapEnvelope) + ").tokens.filter(function (token) { return token.type === 'attr'; }).map(function (token) { return token.text; })");

			CollectionAssert.AreEqual(new[] { "soapenv:Envelope", "soapenv:Body", "a:Op", "a:Op", "soapenv:Body", "soapenv:Envelope" }, Strings(tags));
			CollectionAssert.AreEqual(new[] { "xmlns:soapenv", "xmlns:a", "id" }, Strings(attributes));
		}

		// ------------------------------------------------------------------
		// tokenizeXml: malformed input
		// ------------------------------------------------------------------
		[TestMethod]
		public void TokenizeXml_AnUnterminatedCommentRunsToTheEnd()
		{
			CollectionAssert.AreEqual(new[] { "comment|<!-- never closed <a>" }, Tokens("<!-- never closed <a>"));
			CollectionAssert.AreEqual(new[] { "comment|<!--" }, Tokens("<!--"));
			CollectionAssert.AreEqual(new[] { "comment|<!--->" }, Tokens("<!--->"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "punct|>", "comment|<!-- x --" }, Tokens("<a><!-- x --"));
		}

		[TestMethod]
		public void TokenizeXml_AnUnterminatedCdataRunsToTheEnd()
		{
			CollectionAssert.AreEqual(new[] { "cdata|<![CDATA[x ]] y" }, Tokens("<![CDATA[x ]] y"));
			CollectionAssert.AreEqual(new[] { "cdata|<![CDATA[" }, Tokens("<![CDATA["));
		}

		[TestMethod]
		public void TokenizeXml_AnUnterminatedDeclarationOrDoctypeRunsToTheEnd()
		{
			CollectionAssert.AreEqual(new[] { "decl|<?xml version=\"1.0\"" }, Tokens("<?xml version=\"1.0\""));
			CollectionAssert.AreEqual(new[] { "decl|<?" }, Tokens("<?"));
			CollectionAssert.AreEqual(new[] { "doctype|<!DOCTYPE a [ <!ENTITY e \"v\">" }, Tokens("<!DOCTYPE a [ <!ENTITY e \"v\">"));
			CollectionAssert.AreEqual(new[] { "doctype|<!DOCTYPE a \"x>" }, Tokens("<!DOCTYPE a \"x>"));
			CollectionAssert.AreEqual(new[] { "doctype|<!" }, Tokens("<!"));
		}

		[TestMethod]
		public void TokenizeXml_AnUnterminatedAttributeValueRunsToTheEnd()
		{
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "attr|b", "punct|=", "string|\"unterminated <c> d" }, Tokens("<a b=\"unterminated <c> d"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "attr|b", "punct|=", "string|'" }, Tokens("<a b='"));
		}

		[TestMethod]
		public void TokenizeXml_AnUnterminatedTagEndsAtTheNextAngleBracketOrTheEnd()
		{
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a" }, Tokens("<a"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "attr|b" }, Tokens("<a b"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "punct|<", "tag|b", "punct|>" }, Tokens("<a <b>"));
			CollectionAssert.AreEqual(new[] { "punct|</" }, Tokens("</"));
			CollectionAssert.AreEqual(new[] { "punct|</", "tag|a" }, Tokens("</a"));
		}

		[TestMethod]
		public void TokenizeXml_AnAngleBracketThatStartsNoMarkupIsText()
		{
			CollectionAssert.AreEqual(new[] { "text|<" }, Tokens("<"));
			CollectionAssert.AreEqual(new[] { "text|<>" }, Tokens("<>"));
			CollectionAssert.AreEqual(new[] { "text|< a>" }, Tokens("< a>"));
			CollectionAssert.AreEqual(new[] { "text|a < b > c" }, Tokens("a < b > c"));
			CollectionAssert.AreEqual(new[] { "text|1 <2" }, Tokens("1 <2"));
			CollectionAssert.AreEqual(new[] { "text|==\"<>" }, Tokens("==\"<>"));
		}

		[TestMethod]
		public void TokenizeXml_AttributesWithoutQuotesOrValuesAreStillTokens()
		{
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "attr|b", "punct|=", "attr|c", "text| ", "attr|d", "punct|>" }, Tokens("<a b=c d>"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "attr|b", "punct|=", "string|\"1\"", "attr|c", "punct|=", "string|\"2\"", "punct|>" }, Tokens("<a b=\"1\"c=\"2\">"));
			CollectionAssert.AreEqual(new[] { "punct|<", "tag|a", "text| ", "punct|=", "punct|>" }, Tokens("<a =>"));
			CollectionAssert.AreEqual(new[] { "punct|</", "tag|a", "text| ", "attr|b", "punct|>" }, Tokens("</a b>"));
		}

		[TestMethod]
		public void TokenizeXml_PlainTextAndEmptyInput()
		{
			CollectionAssert.AreEqual(new[] { "text|plain" }, Tokens("plain"));
			CollectionAssert.AreEqual(Array.Empty<string>(), Tokens(string.Empty));
		}

		[TestMethod]
		public void TokenizeXml_EmptyInputHasNoTokens()
		{
			var result = Call("tokenizeXml", Lit(string.Empty));

			AssertOk(result);
			Assert.AreEqual(0, result.GetProperty("tokens").GetArrayLength());
		}

		// ------------------------------------------------------------------
		// tokenizeXml: the tokens always add up to the input
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("")]
		[DataRow("plain text")]
		[DataRow("<a/>")]
		[DataRow("<a:b x=\"1\" y='2'>t</a:b>")]
		[DataRow("<?xml version=\"1.0\" encoding=\"utf-8\"?><r/>")]
		[DataRow("<?xml version=\"1.0\"?><!-- c --><!DOCTYPE r [<!ENTITY e \"v>\">]><r>&e;<![CDATA[a]]b]]></r>")]
		[DataRow("<a href=\"x>y\" t='a\"b<c'/>")]
		[DataRow("<!-- a -- b --><x/>")]
		[DataRow("<!-- - --><!----><!--->")]
		[DataRow("<![CDATA[a ]] b ]> c]]>")]
		[DataRow("<![CDATA[x")]
		[DataRow("<!DOCTYPE a [<!ENTITY e \"v>\">]><a/>")]
		[DataRow("<!DOCTYPE html>")]
		[DataRow("<a b=\"unterminated")]
		[DataRow("<a")]
		[DataRow("<")]
		[DataRow("</")]
		[DataRow("<>")]
		[DataRow("< a>")]
		[DataRow("<a <b>")]
		[DataRow("<!--")]
		[DataRow("<?pi")]
		[DataRow("a < b > c")]
		[DataRow("<a b = \"1\" / >")]
		[DataRow("<a b=c d>")]
		[DataRow("<a b=\"1\"c=\"2\">")]
		[DataRow("<a>&amp;&lt;</a>")]
		[DataRow("<\u00E9l \u00E4=\"\u00FC\"/>")]
		[DataRow("<a>\uD83D\uDE00</a>")]
		[DataRow("<<<<>>>>")]
		[DataRow("<a\"'=/>>")]
		[DataRow("<!<!<!")]
		[DataRow("-->]]>?>")]
		public void TokenizeXml_TheTokensAddUpToTheInput(string text)
		{
			Assert.IsTrue(TokensAddUp(Lit(text)), "The tokens of " + text + " do not add up to the input.");
		}

		[TestMethod]
		[DataRow("'<?xml version=\"1.0\"?>' + String.fromCharCode(10) + '<r/>' + String.fromCharCode(10)")]
		[DataRow("'<a' + String.fromCharCode(10, 9) + 'b=' + String.fromCharCode(34) + '1' + String.fromCharCode(34, 13, 10) + '/>'")]
		[DataRow("'<a>' + String.fromCharCode(55357) + '</a>'")]
		[DataRow("'<a>' + String.fromCharCode(56832) + '</a>'")]
		[DataRow("'<a>' + String.fromCharCode(55357, 56832) + '</a>'")]
		[DataRow("String.fromCharCode(65279) + '<a/>'")]
		[DataRow("'<a>' + String.fromCharCode(0) + '</a>'")]
		[DataRow("'<' + String.fromCharCode(55357)")]
		[DataRow("'<a ' + String.fromCharCode(55357) + '=1>'")]
		[DataRow("'<a b=' + String.fromCharCode(34) + String.fromCharCode(10) + String.fromCharCode(34) + '>'")]
		public void TokenizeXml_TheTokensAddUpToTheInputWhenItHasControlCharactersOrBrokenSurrogates(string textSource)
		{
			Assert.IsTrue(TokensAddUp(textSource), "The tokens of " + textSource + " do not add up to the input.");
		}

		[TestMethod]
		[DataRow(1)]
		[DataRow(2)]
		[DataRow(3)]
		public void TokenizeXml_TheTokensAddUpToTheInputForRandomMarkupLikeText(int seed)
		{
			var script = @"(function (seed) {
				var state = seed * 7919 + 17;
				function next() { state = (state * 1103515245 + 12345) % 2147483648; return state / 2147483648; }
				var pieces = ['<', '>', '/', '=', String.fromCharCode(34), String.fromCharCode(39), ' ', String.fromCharCode(10), '!', '-', '?', '[', ']', 'a', 'b', ':', '&', 'C', 'D', '<!--', '-->', '<![CDATA[', ']]>', '<?', '?>', '<!DOCTYPE', '</', '/>', String.fromCharCode(233), String.fromCharCode(55357, 56832)];
				var types = ['decl', 'comment', 'cdata', 'doctype', 'punct', 'tag', 'attr', 'string', 'text'];
				var bad = [];
				for (var run = 0; run < 150; run += 1) {
					var text = '';
					var count = Math.floor(next() * 24);
					for (var index = 0; index < count; index += 1) { text += pieces[Math.floor(next() * pieces.length)]; }
					var result = window.SoapUiWsdl.tokenizeXml(text);
					var good = result.ok && result.tokens.map(function (token) { return token.text; }).join('') === text && result.tokens.every(function (token) { return token.text !== '' && types.indexOf(token.type) !== -1; });
					if (!good) { bad.push(text); }
				}
				return bad;
			}(" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "))";

			var bad = Strings(SoapUiJavaScriptHost.Evaluate(script));

			Assert.AreEqual(0, bad.Length, "These inputs were not tokenized without loss: " + string.Join(" || ", bad));
		}

		[TestMethod]
		public void TokenizeXml_EveryTokenTypeIsUsedAndNoOtherOne()
		{
			var text = "<?xml version=\"1.0\"?><!DOCTYPE r><!-- c --><r a=\"1\" b='2'>t<![CDATA[x]]><s/></r>";

			var types = Strings(SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.tokenizeXml(" + Lit(text) + ").tokens.map(function (token) { return token.type; })"));

			Assert.IsTrue(types.All(type => TokenTypes.Contains(type)), "Unexpected token type in: " + string.Join(", ", types));
			CollectionAssert.AreEquivalent(TokenTypes, types.Distinct().ToArray());
		}

		[TestMethod]
		public void TokenizeXml_ManyElementsGiveTheExpectedNumberOfTokens()
		{
			var count = Call("tokenizeXml", "'<r>' + new Array(201).join('<a b=\"1\">x</a>') + '</r>'").GetProperty("tokens").GetArrayLength();

			// <r> is 3 tokens, each of the 200 elements 11 and </r> 3 more.
			Assert.AreEqual(3 + (200 * 11) + 3, count);
		}

		[TestMethod]
		public void TokenizeXml_EveryTokenIsAPlainObjectWithTypeAndText()
		{
			var keys = SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.tokenizeXml('<a b=\"1\">t</a>').tokens.map(function (token) { return Object.keys(token).sort().join(',') + ':' + typeof token.type + ':' + typeof token.text; })");

			Assert.IsTrue(Strings(keys).All(key => key == "text,type:string:string"), "Unexpected token shape: " + keys);
		}

		// ------------------------------------------------------------------
		// tokenizeXml: size limit and arguments
		// ------------------------------------------------------------------
		[TestMethod]
		public void TokenizeXml_ATextOverTheDefaultLimitIsRefused()
		{
			var result = SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.tokenizeXml(new Array(262146).join('x'))");

			AssertFailed(result);
			Assert.AreEqual("The text is too large to highlight.", result.GetProperty("error").GetString());
		}

		[TestMethod]
		public void TokenizeXml_ATextAtTheDefaultLimitIsTokenized()
		{
			var result = SoapUiJavaScriptHost.Evaluate("(function () { var result = window.SoapUiWsdl.tokenizeXml(new Array(262145).join('x')); return [result.ok, result.tokens.length, result.tokens[0].text.length]; }())");

			Assert.IsTrue(result[0].GetBoolean());
			Assert.AreEqual(1, result[1].GetInt32());
			Assert.AreEqual(262144, result[2].GetInt32());
		}

		[TestMethod]
		public void TokenizeXml_ACustomLimitCountsCharacters()
		{
			AssertFailed(Call("tokenizeXml", Lit("abcdef"), "5"));
			AssertOk(Call("tokenizeXml", Lit("abcde"), "5"));
			AssertOk(Call("tokenizeXml", Lit("abcdef"), "6"));
			AssertOk(Call("tokenizeXml", Lit(string.Empty), "1"));
			Assert.AreEqual("The text is too large to highlight.", Call("tokenizeXml", Lit("<a/>"), "3").GetProperty("error").GetString());
		}

		[TestMethod]
		public void TokenizeXml_ACustomLimitCanBeHigherThanTheDefault()
		{
			var result = SoapUiJavaScriptHost.Evaluate("(function () { var text = new Array(300001).join('x'); return [window.SoapUiWsdl.tokenizeXml(text).ok, window.SoapUiWsdl.tokenizeXml(text, 300000).ok]; }())");

			Assert.IsFalse(result[0].GetBoolean());
			Assert.IsTrue(result[1].GetBoolean());
		}

		[TestMethod]
		[DataRow("0")]
		[DataRow("-1")]
		[DataRow("NaN")]
		[DataRow("Infinity")]
		[DataRow("'5'")]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("{}")]
		[DataRow("true")]
		public void TokenizeXml_ALimitThatIsNotAPositiveNumberMeansTheDefault(string limitSource)
		{
			AssertOk(Call("tokenizeXml", Lit("<a>some text</a>"), limitSource));
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("5")]
		[DataRow("true")]
		[DataRow("{}")]
		[DataRow("['<a/>']")]
		public void TokenizeXml_ATextThatIsNotAStringFails(string textSource)
		{
			AssertFailed(Call("tokenizeXml", textSource));
		}

		// ------------------------------------------------------------------
		// findMatches
		// ------------------------------------------------------------------
		[TestMethod]
		public void FindMatches_IsCaseInsensitiveByDefault()
		{
			var result = Call("findMatches", Lit("Hello hello HELLO"), Lit("hello"));

			AssertOk(result);
			CollectionAssert.AreEqual(new[] { "0-5", "6-11", "12-17" }, Ranges(result));
			Assert.IsFalse(result.GetProperty("truncated").GetBoolean());
		}

		[TestMethod]
		public void FindMatches_CanBeCaseSensitive()
		{
			var result = Call("findMatches", Lit("Hello hello HELLO"), Lit("hello"), "{ caseSensitive: true }");

			CollectionAssert.AreEqual(new[] { "6-11" }, Ranges(result));
		}

		[TestMethod]
		public void FindMatches_TheCaseOfTheQueryDoesNotMatterWhenInsensitive()
		{
			CollectionAssert.AreEqual(new[] { "0-5", "6-11", "12-17" }, Ranges(Call("findMatches", Lit("Hello hello HELLO"), Lit("HeLLo"))));
			CollectionAssert.AreEqual(new[] { "0-5" }, Ranges(Call("findMatches", Lit("Hello hello HELLO"), Lit("Hello"), "{ caseSensitive: true }")));
			CollectionAssert.AreEqual(Array.Empty<string>(), Ranges(Call("findMatches", Lit("Hello hello HELLO"), Lit("HeLLo"), "{ caseSensitive: true }")));
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("{}")]
		[DataRow("5")]
		[DataRow("'yes'")]
		[DataRow("{ caseSensitive: 'yes' }")]
		[DataRow("{ caseSensitive: 1 }")]
		[DataRow("{ caseSensitive: false }")]
		[DataRow("[true]")]
		public void FindMatches_OptionsThatDoNotSayCaseSensitiveTrueMeanInsensitive(string optionsSource)
		{
			var result = Call("findMatches", Lit("Aa"), Lit("a"), optionsSource);

			CollectionAssert.AreEqual(new[] { "0-1", "1-2" }, Ranges(result));
		}

		[TestMethod]
		public void FindMatches_MatchesDoNotOverlap()
		{
			CollectionAssert.AreEqual(new[] { "0-2", "2-4" }, Ranges(Call("findMatches", Lit("aaaa"), Lit("aa"))));
			CollectionAssert.AreEqual(new[] { "0-2", "2-4" }, Ranges(Call("findMatches", Lit("aaaaa"), Lit("aa"))));
			CollectionAssert.AreEqual(new[] { "0-3" }, Ranges(Call("findMatches", Lit("aaaaa"), Lit("aaa"))));
			CollectionAssert.AreEqual(new[] { "0-3" }, Ranges(Call("findMatches", Lit("ababa"), Lit("aba"))));
			CollectionAssert.AreEqual(new[] { "0-3", "3-6" }, Ranges(Call("findMatches", Lit("abcabc"), Lit("abc"))));
		}

		[TestMethod]
		public void FindMatches_RangesAreInOrderAndInsideTheText()
		{
			var text = "one two one three one";
			var result = Call("findMatches", Lit(text), Lit("one"));

			var ranges = result.GetProperty("ranges").EnumerateArray().Select(range => (Start: range.GetProperty("start").GetInt32(), End: range.GetProperty("end").GetInt32())).ToArray();

			Assert.AreEqual(3, ranges.Length);
			for (var index = 0; index < ranges.Length; index++)
			{
				Assert.AreEqual("one", text.Substring(ranges[index].Start, ranges[index].End - ranges[index].Start));
				if (index > 0)
				{
					Assert.IsTrue(ranges[index].Start >= ranges[index - 1].End, "The ranges overlap or are out of order.");
				}
			}
		}

		[TestMethod]
		public void FindMatches_AnEmptyQueryFindsNothing()
		{
			var result = Call("findMatches", Lit("abc"), Lit(string.Empty));

			AssertOk(result);
			Assert.AreEqual(0, result.GetProperty("ranges").GetArrayLength());
			Assert.IsFalse(result.GetProperty("truncated").GetBoolean());
			AssertOk(Call("findMatches", Lit(string.Empty), Lit(string.Empty)));
		}

		[TestMethod]
		public void FindMatches_AnEmptyTextOrNoMatchFindsNothing()
		{
			CollectionAssert.AreEqual(Array.Empty<string>(), Ranges(Call("findMatches", Lit(string.Empty), Lit("a"))));
			CollectionAssert.AreEqual(Array.Empty<string>(), Ranges(Call("findMatches", Lit("abc"), Lit("d"))));
			CollectionAssert.AreEqual(Array.Empty<string>(), Ranges(Call("findMatches", Lit("abc"), Lit("abcd"))));
		}

		[TestMethod]
		[DataRow("a a.c abc", "a.c", "2-5")]
		[DataRow("x.*y", ".*", "1-3")]
		[DataRow("(a)", "(a", "0-2")]
		[DataRow("a[b]c", "[b", "1-3")]
		[DataRow("\\d", "\\", "0-1")]
		[DataRow("$1", "$", "0-1")]
		[DataRow("^a$", "^a$", "0-3")]
		[DataRow("a+b", "a+", "0-2")]
		[DataRow("a|b", "|", "1-2")]
		[DataRow("a?b", "?", "1-2")]
		public void FindMatches_TheQueryIsLiteralTextNotARegularExpression(string text, string query, string expectedRange)
		{
			CollectionAssert.AreEqual(new[] { expectedRange }, Ranges(Call("findMatches", Lit(text), Lit(query))));
		}

		[TestMethod]
		[DataRow("aaa", "a+")]
		[DataRow("abc", "a.c")]
		[DataRow("abc", "[a-c]")]
		[DataRow("abc", "^a")]
		[DataRow("abc", "c$")]
		[DataRow("abc", "(a|b)")]
		public void FindMatches_APatternThatWouldMatchAsARegularExpressionFindsNothing(string text, string query)
		{
			CollectionAssert.AreEqual(Array.Empty<string>(), Ranges(Call("findMatches", Lit(text), Lit(query))));
		}

		[TestMethod]
		public void FindMatches_TheQueryCanHaveLineBreaksAndMarkup()
		{
			CollectionAssert.AreEqual(new[] { "1-4" }, Ranges(Call("findMatches", Lit("x\r\ny"), Lit("\r\ny"))));
			CollectionAssert.AreEqual(new[] { "3-9" }, Ranges(Call("findMatches", Lit("<a><b/></a>"), Lit("<b/></"))));
			CollectionAssert.AreEqual(new[] { "6-11" }, Ranges(Call("findMatches", Lit("a=\"1\" 'x&y' "), Lit("'x&y'"))));
		}

		[TestMethod]
		public void FindMatches_AQueryOfTwoHundredCharactersIsAllowedAndOneMoreIsNot()
		{
			var allowed = Call("findMatches", Lit(new string('a', 300)), Lit(new string('a', 200)));
			var refused = Call("findMatches", Lit(new string('a', 300)), Lit(new string('a', 201)));

			AssertOk(allowed);
			CollectionAssert.AreEqual(new[] { "0-200" }, Ranges(allowed));
			AssertFailed(refused);
			StringAssert.Contains(refused.GetProperty("error").GetString(), "200");
		}

		[TestMethod]
		public void FindMatches_StopsAtOneThousandRangesAndSaysSo()
		{
			var result = Call("findMatches", Lit(new string('a', 1500)), Lit("a"));

			AssertOk(result);
			Assert.AreEqual(1000, result.GetProperty("ranges").GetArrayLength());
			Assert.IsTrue(result.GetProperty("truncated").GetBoolean());
			Assert.AreEqual(999, result.GetProperty("ranges")[999].GetProperty("start").GetInt32());
			Assert.AreEqual(1000, result.GetProperty("ranges")[999].GetProperty("end").GetInt32());
		}

		[TestMethod]
		public void FindMatches_ExactlyOneThousandRangesIsNotTruncated()
		{
			var exact = Call("findMatches", Lit(new string('a', 1000)), Lit("a"));
			var oneMore = Call("findMatches", Lit(new string('a', 1001)), Lit("a"));

			Assert.AreEqual(1000, exact.GetProperty("ranges").GetArrayLength());
			Assert.IsFalse(exact.GetProperty("truncated").GetBoolean());
			Assert.AreEqual(1000, oneMore.GetProperty("ranges").GetArrayLength());
			Assert.IsTrue(oneMore.GetProperty("truncated").GetBoolean());
		}

		[TestMethod]
		[DataRow("null", "'a'")]
		[DataRow("'a'", "null")]
		[DataRow("5", "'a'")]
		[DataRow("'a'", "5")]
		[DataRow("undefined", "undefined")]
		[DataRow("['a']", "'a'")]
		[DataRow("'a'", "['a']")]
		[DataRow("{}", "{}")]
		[DataRow("'abc'", "/b/")]
		public void FindMatches_ArgumentsThatAreNotStringsFail(string textSource, string querySource)
		{
			AssertFailed(Call("findMatches", textSource, querySource));
		}

		[TestMethod]
		public void FindMatches_RangesOfTheInsensitiveSearchPointIntoTheOriginalText()
		{
			// The capital I with a dot has a longer lower case in JavaScript, which must not shift the positions after it.
			var text = "\u0130\u0130 x \u0130X";

			var result = Call("findMatches", Lit(text), Lit("x"));

			CollectionAssert.AreEqual(new[] { "3-4", "6-7" }, Ranges(result));
			foreach (var range in Ranges(result))
			{
				var bounds = range.Split('-').Select(int.Parse).ToArray();
				Assert.AreEqual("x", text.Substring(bounds[0], bounds[1] - bounds[0]).ToLowerInvariant());
			}
		}

		[TestMethod]
		public void FindMatches_AnUppercaseDottedIFindsItself()
		{
			CollectionAssert.AreEqual(new[] { "0-1", "2-3" }, Ranges(Call("findMatches", Lit("\u0130 \u0130"), Lit("\u0130"))));
			CollectionAssert.AreEqual(new[] { "0-1" }, Ranges(Call("findMatches", Lit("\u0130"), Lit("\u0130"), "{ caseSensitive: true }")));
		}

		[TestMethod]
		public void FindMatches_TheGermanSharpSDoesNotMatchSs()
		{
			// The lower case mapping keeps the sharp s as it is. It is a simple case mapping, not a full case folding.
			CollectionAssert.AreEqual(Array.Empty<string>(), Ranges(Call("findMatches", Lit("Stra\u00DFe"), Lit("SS"))));
			CollectionAssert.AreEqual(Array.Empty<string>(), Ranges(Call("findMatches", Lit("STRASSE"), Lit("\u00DF"))));
			CollectionAssert.AreEqual(new[] { "4-5" }, Ranges(Call("findMatches", Lit("Stra\u00DFe"), Lit("\u00DF"))));
			CollectionAssert.AreEqual(new[] { "4-5" }, Ranges(Call("findMatches", Lit("STRA\u00DFE"), Lit("\u00DF"))));
		}

		[TestMethod]
		public void FindMatches_PositionsAfterASharpSAreExact()
		{
			CollectionAssert.AreEqual(new[] { "5-6" }, Ranges(Call("findMatches", Lit("Stra\u00DFe"), Lit("E"))));
			CollectionAssert.AreEqual(new[] { "0-1", "2-3", "4-5" }, Ranges(Call("findMatches", Lit("e\u00DFe\u00DFE"), Lit("e"))));
		}

		[TestMethod]
		public void FindMatches_AccentedLettersMatchTheirOtherCase()
		{
			CollectionAssert.AreEqual(new[] { "0-1", "2-3" }, Ranges(Call("findMatches", Lit("\u00C9 \u00E9"), Lit("\u00E9"))));
			CollectionAssert.AreEqual(new[] { "0-1", "2-3" }, Ranges(Call("findMatches", Lit("\u00C9 \u00E9"), Lit("\u00C9"))));
			CollectionAssert.AreEqual(new[] { "2-3" }, Ranges(Call("findMatches", Lit("\u00C9 \u00E9"), Lit("\u00E9"), "{ caseSensitive: true }")));
		}

		[TestMethod]
		public void FindMatches_AnAstralLetterIsOneCharacterOfTwoUnits()
		{
			// U+10400 and U+10428 are the capital and the small Deseret long I. Both are two UTF-16 units long.
			var text = "\uD801\uDC00 x \uD801\uDC28";

			CollectionAssert.AreEqual(new[] { "0-2", "5-7" }, Ranges(Call("findMatches", Lit(text), Lit("\uD801\uDC28"))));
			CollectionAssert.AreEqual(new[] { "0-2" }, Ranges(Call("findMatches", Lit(text), Lit("\uD801\uDC00"), "{ caseSensitive: true }")));
			CollectionAssert.AreEqual(new[] { "3-4" }, Ranges(Call("findMatches", Lit(text), Lit("x"))));
		}

		[TestMethod]
		public void FindMatches_InsensitivePositionsMatchTheOriginalForAnyText()
		{
			var script = @"(function () {
				var text = 'A' + String.fromCharCode(304) + 'b' + String.fromCharCode(223) + 'C ' + String.fromCharCode(55297, 56320) + 'x' + String.fromCharCode(201) + 'x';
				var result = window.SoapUiWsdl.findMatches(text, 'x');
				return result.ranges.map(function (range) { return text.slice(range.start, range.end); });
			}())";

			var found = Strings(SoapUiJavaScriptHost.Evaluate(script));

			CollectionAssert.AreEqual(new[] { "x", "x" }, found);
		}

		[TestMethod]
		public void FindMatches_RangesNeverOverlapAndAreInOrderForRandomText()
		{
			var script = @"(function () {
				var state = 12345;
				function next() { state = (state * 1103515245 + 12345) % 2147483648; return state / 2147483648; }
				var letters = ['a', 'A', 'b', ' ', String.fromCharCode(304), String.fromCharCode(223), String.fromCharCode(55297, 56320)];
				var problems = [];
				for (var run = 0; run < 60; run += 1) {
					var text = '';
					var count = Math.floor(next() * 30);
					for (var index = 0; index < count; index += 1) { text += letters[Math.floor(next() * letters.length)]; }
					var result = window.SoapUiWsdl.findMatches(text, 'a');
					var last = 0;
					result.ranges.forEach(function (range) {
						if (range.start < last || range.end <= range.start || range.end > text.length || text.slice(range.start, range.end).toLowerCase() !== 'a') { problems.push(text); }
						last = range.end;
					});
				}
				return problems;
			}())";

			var problems = Strings(SoapUiJavaScriptHost.Evaluate(script));

			Assert.AreEqual(0, problems.Length, "Bad ranges for: " + string.Join(" || ", problems));
		}

		[TestMethod]
		public void FindMatches_ResultHasExactlyTheDocumentedProperties()
		{
			var result = Call("findMatches", Lit("a"), Lit("a"));

			CollectionAssert.AreEquivalent(new[] { "ok", "ranges", "truncated" }, result.EnumerateObject().Select(property => property.Name).ToArray());
			CollectionAssert.AreEquivalent(new[] { "start", "end" }, result.GetProperty("ranges")[0].EnumerateObject().Select(property => property.Name).ToArray());
		}

		[TestMethod]
		public void FindMatches_FindsMatchesInXmlText()
		{
			var xml = "<a>\n\t<b>Value</b>\n</a>";

			CollectionAssert.AreEqual(new[] { "8-13" }, Ranges(Call("findMatches", Lit(xml), Lit("VALUE"))));
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: the result
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildFormTemplate_ReturnsTheOperationNameTheBodyPartsAndTheWarnings()
		{
			var result = TemplateOf(OpElement("<xs:element name='A' type='xs:string'/>"));

			AssertOk(result);
			var template = result.GetProperty("template");
			CollectionAssert.AreEqual(new[] { "bodyParts", "operationName", "warnings" }, template.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
			Assert.AreEqual("Op", template.GetProperty("operationName").GetString());
			Assert.AreEqual(1, template.GetProperty("bodyParts").GetArrayLength());
			Assert.AreEqual(0, template.GetProperty("warnings").GetArrayLength());
		}

		[TestMethod]
		public void BuildFormTemplate_TheBodyPartIsTheGlobalElementOfTheInputMessage()
		{
			var template = TemplateOf(OpElement("<xs:element name='A' type='xs:string'/><xs:element name='B' type='xs:int'/>"));

			Assert.AreEqual(
				Join("Op complex [1..1]", "  A simple string [1..1]", "  B simple int [1..1]"),
				Render(template));
			var part = template.GetProperty("template").GetProperty("bodyParts")[0];
			Assert.AreEqual("Op", part.GetProperty("name").GetString());
			Assert.AreEqual(DefaultNamespace, part.GetProperty("ns").GetString());
			Assert.AreEqual("complex", part.GetProperty("kind").GetString());
			Assert.AreEqual(JsonValueKind.Null, part.GetProperty("type").ValueKind);
		}

		[TestMethod]
		public void BuildFormTemplate_EveryNodeAndEveryAttributeHasExactlyTheDocumentedShape()
		{
			var template = TemplateOf(
				OpElement(
					"<xs:element name='A' type='xs:string' nillable='true'/><xs:element name='B' type='tns:Color' minOccurs='0' maxOccurs='unbounded'/><xs:element name='C'><xs:complexType><xs:sequence><xs:element name='D' type='xs:int'/></xs:sequence><xs:attribute name='e' type='xs:int' use='required' fixed='1'/></xs:complexType></xs:element>",
					"<xs:attribute name='x' type='tns:Color'/>"),
				ColorType);
			var checkedNodes = 0;

			void CheckNode(JsonElement node)
			{
				checkedNodes++;
				CollectionAssert.AreEqual(
					new[] { "anyContent", "attributes", "children", "choiceGroup", "kind", "max", "min", "name", "nillable", "note", "ns", "truncated", "type" },
					node.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
				Assert.AreEqual(JsonValueKind.String, node.GetProperty("name").ValueKind);
				Assert.AreEqual(JsonValueKind.String, node.GetProperty("ns").ValueKind);
				Assert.AreEqual(JsonValueKind.String, node.GetProperty("note").ValueKind);
				Assert.AreEqual(JsonValueKind.Number, node.GetProperty("min").ValueKind);
				Assert.IsTrue(node.GetProperty("max").ValueKind == JsonValueKind.Number || node.GetProperty("max").ValueKind == JsonValueKind.Null);
				Assert.IsTrue(node.GetProperty("choiceGroup").ValueKind == JsonValueKind.String || node.GetProperty("choiceGroup").ValueKind == JsonValueKind.Null);
				foreach (var flag in new[] { "nillable", "anyContent", "truncated" })
				{
					Assert.IsTrue(node.GetProperty(flag).ValueKind == JsonValueKind.True || node.GetProperty(flag).ValueKind == JsonValueKind.False, flag + " must be a boolean.");
				}

				var kind = node.GetProperty("kind").GetString();
				Assert.IsTrue(kind == "simple" || kind == "complex");
				if (kind == "simple")
				{
					CollectionAssert.AreEqual(new[] { "enumValues", "pattern", "xsd" }, node.GetProperty("type").EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
					Assert.AreEqual(0, node.GetProperty("children").GetArrayLength(), "A simple node has no children.");
				}
				else
				{
					Assert.AreEqual(JsonValueKind.Null, node.GetProperty("type").ValueKind);
				}

				foreach (var attribute in node.GetProperty("attributes").EnumerateArray())
				{
					CollectionAssert.AreEqual(new[] { "fixed", "name", "ns", "required", "type" }, attribute.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
					CollectionAssert.AreEqual(new[] { "enumValues", "pattern", "xsd" }, attribute.GetProperty("type").EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
				}

				foreach (var child in node.GetProperty("children").EnumerateArray())
				{
					CheckNode(child);
				}
			}

			CheckNode(template.GetProperty("template").GetProperty("bodyParts")[0]);

			Assert.AreEqual(5, checkedNodes);
		}

		[TestMethod]
		public void BuildFormTemplate_TheResultIsPlainDataThatSurvivesAJsonRoundTrip()
		{
			var expression = "(function () { var wsdl = " + Lit(Wsdl(OpElement("<xs:element name='A' type='tns:Color'/><xs:element name='B' type='xs:int' minOccurs='0' maxOccurs='unbounded'/>", "<xs:attribute name='x' type='xs:int'/>") + ColorType, "Op")) + "; var result = window.SoapUiWsdl.buildFormTemplate(window.SoapUiWsdl.parse(wsdl).model, { name: 'Op' }, '1.1'); return JSON.stringify(result) === JSON.stringify(JSON.parse(JSON.stringify(result))) && JSON.stringify(result).indexOf('undefined') === -1 && Object.getPrototypeOf(result.template.bodyParts[0]) === Object.prototype; }())";

			Assert.IsTrue(WithDom(expression).GetBoolean());
		}

		[TestMethod]
		public void BuildFormTemplate_TheSameModelGivesTheSameTemplateEveryTime()
		{
			var wsdl = Wsdl(OpElement("<xs:element name='A' type='tns:Loop'/>") + "<xs:complexType name='Loop'><xs:sequence><xs:element name='Next' type='tns:Loop' minOccurs='0'/></xs:sequence></xs:complexType>", "Op");
			var expression = "(function () { var api = window.SoapUiWsdl; var model = api.parse(" + Lit(wsdl) + ").model; var first = JSON.stringify(api.buildFormTemplate(model, { name: 'Op' }, '1.1')); var second = JSON.stringify(api.buildFormTemplate(model, { name: 'Op' }, '1.1')); return first === second; }())";

			Assert.IsTrue(WithDom(expression).GetBoolean());
		}

		[TestMethod]
		public void BuildFormTemplate_TheSoapVersionDoesNotChangeTheTemplate()
		{
			var wsdl = Wsdl(OpElement("<xs:element name='A' type='xs:string'/>"), "Op");
			var expression = "(function () { var api = window.SoapUiWsdl; var model = api.parse(" + Lit(wsdl) + ").model; return JSON.stringify(api.buildFormTemplate(model, { name: 'Op' }, '1.1')) === JSON.stringify(api.buildFormTemplate(model, { name: 'Op' }, '1.2')); }())";

			Assert.IsTrue(WithDom(expression).GetBoolean());
		}

		[TestMethod]
		public void BuildFormTemplate_AnOperationWithSeveralPartsGivesOneBodyPartForEach()
		{
			var wsdl = WsdlCustom(
				"<xs:schema elementFormDefault='qualified' targetNamespace='urn:t'><xs:element name='First'><xs:complexType><xs:sequence><xs:element name='A' type='xs:string'/></xs:sequence></xs:complexType></xs:element><xs:element name='Second' type='xs:int'/></xs:schema>",
				"<wsdl:message name='OpIn'><wsdl:part name='a' element='tns:First'/><wsdl:part name='b' element='tns:Second'/></wsdl:message><wsdl:portType name='P'><wsdl:operation name='Op'><wsdl:input message='tns:OpIn'/></wsdl:operation></wsdl:portType>");

			var template = Template(wsdl, "Op");

			Assert.AreEqual(Join("First complex [1..1]", "  A simple string [1..1]", "Second simple int [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AGlobalElementWithASimpleTypeIsASimpleBodyPart()
		{
			var template = TemplateOf("<xs:element name='Op' type='xs:int'/>");

			Assert.AreEqual("Op simple int [1..1]", Render(template));
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: operations the sample builder refuses
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("null", "'1.1'", "No operation was given.")]
		[DataRow("undefined", "'1.1'", "No operation was given.")]
		[DataRow("{}", "'1.1'", "No operation was given.")]
		[DataRow("{ name: 5 }", "'1.1'", "No operation was given.")]
		[DataRow("{ name: 'Nope' }", "'1.1'", "The WSDL does not describe the operation Nope.")]
		[DataRow("{ name: 'Op' }", "'2.0'", "Unsupported SOAP version.")]
		[DataRow("{ name: 'Op' }", "''", "Unsupported SOAP version.")]
		[DataRow("{ name: 'Op' }", "null", "Unsupported SOAP version.")]
		[DataRow("{ name: 'Op' }", "undefined", "Unsupported SOAP version.")]
		[DataRow("{ name: 'Op' }", "1.1", "Unsupported SOAP version.")]
		[DataRow("{ name: 'rpc' }", "'1.1'", "This operation uses rpc or encoded style, which the sample generator does not support.")]
		[DataRow("{ name: 'noInput' }", "'1.2'", "The WSDL has no input message for this operation.")]
		[DataRow("{ name: 'empty' }", "'1.1'", "The input message of this operation has no parts.")]
		[DataRow("{ name: 'undeclared' }", "'1.2'", "The WSDL does not declare the element NoSuchElement.")]
		public void BuildFormTemplate_FailsForAnOperationTheEnvelopeBuilderRefuses(string operationSource, string versionSource, string expectedError)
		{
			var wsdl = RefusedOperationsWsdl();

			var result = WithDom("(function () { var api = window.SoapUiWsdl; var model = api.parse(" + Lit(wsdl) + ").model; return [api.buildFormTemplate(model, " + operationSource + ", " + versionSource + "), api.buildEnvelope(model, " + operationSource + ", " + versionSource + ")]; }())");

			AssertFailed(result[0]);
			Assert.AreEqual(expectedError, result[0].GetProperty("error").GetString());
			Assert.AreEqual(result[1].GetProperty("error").GetString(), result[0].GetProperty("error").GetString(), "The envelope builder must give the same error.");
		}

		[TestMethod]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("5")]
		[DataRow("'text'")]
		[DataRow("{}")]
		[DataRow("[]")]
		[DataRow("{ operations: null }")]
		[DataRow("{ operations: new Map() }")]
		public void BuildFormTemplate_FailsWhenTheModelIsNotAModel(string modelSource)
		{
			var result = Call("buildFormTemplate", modelSource, "{ name: 'Op' }", "'1.1'");

			AssertFailed(result);
		}

		[TestMethod]
		public void BuildFormTemplate_FailsWhenTheMessageOfTheOperationIsMissingFromTheModel()
		{
			var model = "{ operations: new Map([['Op', { name: 'Op', inputMessage: 'm' }]]), messages: new Map(), elements: new Map() }";

			var result = Call("buildFormTemplate", model, "{ name: 'Op' }", "'1.1'");

			AssertFailed(result);
			Assert.AreEqual("The WSDL has no input message for this operation.", result.GetProperty("error").GetString());
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: occurrence, nillable and names
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildFormTemplate_MinAndMaxFollowMinOccursAndMaxOccurs()
		{
			var template = TemplateOf(OpElement(
				"<xs:element name='Name' type='xs:string' minOccurs='0'/>"
				+ "<xs:element name='Count' type='xs:int' nillable='true'/>"
				+ "<xs:element name='Tags' type='xs:string' minOccurs='0' maxOccurs='unbounded'/>"
				+ "<xs:element name='Pair' type='xs:string' minOccurs='2' maxOccurs='5'/>"
				+ "<xs:element name='Flag' type='xs:boolean' nillable='1'/>"
				+ "<xs:element name='Off' type='xs:boolean' nillable='false'/>"
				+ "<xs:element name='Odd' type='xs:string' minOccurs='x' maxOccurs='y'/>"
				+ "<xs:element name='Gone' type='xs:string' maxOccurs='0'/>"
				+ "<xs:element name='Many' type='xs:string' maxOccurs='99999999999'/>"
				+ "<xs:element name='Spaced' type='xs:string' minOccurs=' 3 ' maxOccurs=' unbounded '/>"));

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Name simple string [0..1]",
					"  Count simple int [1..1] nillable",
					"  Tags simple string [0..*]",
					"  Pair simple string [2..5]",
					"  Flag simple boolean [1..1] nillable",
					"  Off simple boolean [1..1]",
					"  Odd simple string [1..1]",
					"  Many simple string [1..*]",
					"  Spaced simple string [3..*]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AnElementThatCannotOccurIsLeftOut()
		{
			var template = TemplateOf(OpElement("<xs:element name='Gone' type='xs:string' maxOccurs='0'/><xs:element name='Kept' type='xs:string'/>"));

			Assert.AreEqual(Join("Op complex [1..1]", "  Kept simple string [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_TheNamesOfElementsAreKeptAsTheyAre()
		{
			var template = TemplateOf(OpElement(
				"<xs:element name='__proto__' type='xs:string'/><xs:element name='constructor' type='xs:string'/><xs:element name='toString' type='xs:string'/>"
				+ "<xs:element name='hasOwnProperty' type='xs:string'/><xs:element name='valueOf' type='xs:string'/><xs:element name='a.b-c_d' type='xs:string'/>"
				+ "<xs:element name='\u00E9l\u00E8ve' type='xs:string'/>"));

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  __proto__ simple string [1..1]",
					"  constructor simple string [1..1]",
					"  toString simple string [1..1]",
					"  hasOwnProperty simple string [1..1]",
					"  valueOf simple string [1..1]",
					"  a.b-c_d simple string [1..1]",
					"  \u00E9l\u00E8ve simple string [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AnElementWhoseNameIsNotAValidXmlNameIsSkippedWithAWarning()
		{
			var template = TemplateOf(OpElement(
				"<xs:element name='good' type='xs:string'/><xs:element name='bad name' type='xs:string'/><xs:element name='a:b' type='xs:string'/>"
				+ "<xs:element name='1x' type='xs:string'/><xs:element name='-x' type='xs:string'/><xs:element name='' type='xs:string'/><xs:element name='a&lt;b' type='xs:string'/>"
				+ "<xs:element type='xs:string'/>"));

			Assert.AreEqual(Join("Op complex [1..1] note=\"element without a name skipped\"", "  good simple string [1..1]"), Render(template));
			var warnings = Warnings(template);
			CollectionAssert.AreEqual(
				new[]
				{
					"An element was skipped, the name is not valid XML: bad name",
					"An element was skipped, the name is not valid XML: a:b",
					"An element was skipped, the name is not valid XML: 1x",
					"An element was skipped, the name is not valid XML: -x",
					"An element was skipped, the name is not valid XML: a<b",
				},
				warnings);
		}

		[TestMethod]
		public void BuildFormTemplate_AnElementWithoutANameIsNotedOnItsParent()
		{
			var template = TemplateOf(OpElement("<xs:element type='xs:string'/><xs:element name='B' type='xs:string'/>"));

			Assert.AreEqual(Join("Op complex [1..1] note=\"element without a name skipped\"", "  B simple string [1..1]"), Render(template));
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: namespaces
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildFormTemplate_ElementsAreQualifiedWhenTheSchemaSaysSo()
		{
			var template = Template(Wsdl(OpElement("<xs:element name='A' type='xs:string'/>"), "Op"), "Op");

			var child = template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children")[0];

			Assert.AreEqual(DefaultNamespace, child.GetProperty("ns").GetString());
		}

		[TestMethod]
		public void BuildFormTemplate_LocalElementsAreUnqualifiedWhenTheSchemaDoesNotSayOtherwise()
		{
			var types = "<xs:schema targetNamespace='urn:t'>" + OpElement("<xs:element name='A' type='xs:string'/><xs:element name='B' type='xs:string' form='qualified'/>") + "</xs:schema>";

			var template = Template(WsdlTypes(types, "Op"), "Op");

			Assert.AreEqual(Join("Op complex [1..1]", "  {}A simple string [1..1]", "  B simple string [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AFormAttributeOverridesTheDefaultOfTheSchema()
		{
			var template = TemplateOf(OpElement("<xs:element name='A' type='xs:string' form='unqualified'/><xs:element name='B' type='xs:string' form='qualified'/>"));

			Assert.AreEqual(Join("Op complex [1..1]", "  {}A simple string [1..1]", "  B simple string [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ALocalElementCanCarryItsOwnTargetNamespace()
		{
			var template = TemplateOf(OpElement("<xs:element name='A' type='xs:string' targetNamespace='urn:own'/>"));

			Assert.AreEqual(Join("Op complex [1..1]", "  {urn:own}A simple string [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_TypesFromAnotherSchemaBringTheirOwnNamespace()
		{
			// The DataContract serializer writes one schema for each namespace. The elements of a type live in the namespace of its schema.
			var types = "<xs:schema elementFormDefault='qualified' targetNamespace='urn:t'>"
				+ OpElement("<xs:element name='Item' type='o:Item'/><xs:element name='Names' type='arr:ArrayOfstring' minOccurs='0' nillable='true'/>")
				+ "</xs:schema>"
				+ "<xs:schema elementFormDefault='qualified' targetNamespace='urn:o'><xs:complexType name='Item'><xs:sequence><xs:element name='Label' type='xs:string'/></xs:sequence></xs:complexType></xs:schema>"
				+ "<xs:schema elementFormDefault='qualified' targetNamespace='http://schemas.microsoft.com/2003/10/Serialization/Arrays'><xs:complexType name='ArrayOfstring'><xs:sequence><xs:element name='string' type='xs:string' nillable='true' minOccurs='0' maxOccurs='unbounded'/></xs:sequence></xs:complexType></xs:schema>";

			var template = Template(WsdlTypes(types, "Op"), "Op");

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Item complex [1..1]",
					"    {urn:o}Label simple string [1..1]",
					"  Names complex [0..1] nillable",
					"    {http://schemas.microsoft.com/2003/10/Serialization/Arrays}string simple string [0..*] nillable"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AReferenceToAGlobalElementTakesItsNamespaceAndKeepsItsOwnOccurrence()
		{
			var types = "<xs:schema elementFormDefault='qualified' targetNamespace='urn:t'>"
				+ OpElement("<xs:element ref='o:Shared' minOccurs='0' maxOccurs='3'/><xs:element ref='o:Plain'/>")
				+ "</xs:schema>"
				+ "<xs:schema elementFormDefault='qualified' targetNamespace='urn:o'><xs:element name='Shared'><xs:complexType><xs:sequence><xs:element name='X' type='xs:int'/></xs:sequence></xs:complexType></xs:element><xs:element name='Plain' type='xs:string'/></xs:schema>";

			var template = Template(WsdlTypes(types, "Op"), "Op");

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  {urn:o}Shared complex [0..3]",
					"    {urn:o}X simple int [1..1]",
					"  {urn:o}Plain simple string [1..1]"),
				Render(template));
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: simple types
		// ------------------------------------------------------------------
		[TestMethod]
		[DataRow("string", "string")]
		[DataRow("normalizedString", "string")]
		[DataRow("token", "string")]
		[DataRow("language", "string")]
		[DataRow("NMTOKEN", "string")]
		[DataRow("QName", "string")]
		[DataRow("gYear", "string")]
		[DataRow("anySimpleType", "string")]
		[DataRow("boolean", "boolean")]
		[DataRow("decimal", "decimal")]
		[DataRow("float", "float")]
		[DataRow("double", "double")]
		[DataRow("int", "int")]
		[DataRow("integer", "integer")]
		[DataRow("long", "long")]
		[DataRow("short", "short")]
		[DataRow("byte", "byte")]
		[DataRow("unsignedInt", "unsignedInt")]
		[DataRow("unsignedLong", "unsignedLong")]
		[DataRow("unsignedShort", "unsignedShort")]
		[DataRow("unsignedByte", "unsignedByte")]
		[DataRow("positiveInteger", "positiveInteger")]
		[DataRow("nonNegativeInteger", "nonNegativeInteger")]
		[DataRow("negativeInteger", "negativeInteger")]
		[DataRow("nonPositiveInteger", "nonPositiveInteger")]
		[DataRow("dateTime", "dateTime")]
		[DataRow("date", "date")]
		[DataRow("time", "time")]
		[DataRow("duration", "duration")]
		[DataRow("base64Binary", "base64Binary")]
		[DataRow("hexBinary", "hexBinary")]
		[DataRow("anyURI", "anyURI")]
		public void BuildFormTemplate_ABuiltInTypeGivesItsXsdName(string schemaType, string expectedXsd)
		{
			var template = TemplateOf(OpElement("<xs:element name='V' type='xs:" + schemaType + "'/>"));

			var type = template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children")[0].GetProperty("type");

			Assert.AreEqual(expectedXsd, type.GetProperty("xsd").GetString());
			Assert.AreEqual(JsonValueKind.Null, type.GetProperty("enumValues").ValueKind);
			Assert.AreEqual(JsonValueKind.Null, type.GetProperty("pattern").ValueKind);
		}

		[TestMethod]
		public void BuildFormTemplate_EnumerationsPatternsAndRestrictionsOfRestrictions()
		{
			var extras = ColorType
				+ "<xs:simpleType name='Code'><xs:restriction base='xs:string'><xs:pattern value='[A-Z]{3}'/><xs:maxLength value='3'/></xs:restriction></xs:simpleType>"
				+ "<xs:simpleType name='Wide'><xs:restriction base='xs:long'><xs:enumeration value='1'/><xs:enumeration value='2'/></xs:restriction></xs:simpleType>"
				+ "<xs:simpleType name='Narrow'><xs:restriction base='tns:Wide'><xs:enumeration value='2'/></xs:restriction></xs:simpleType>"
				+ "<xs:simpleType name='Letters'><xs:restriction base='tns:Code'><xs:pattern value='[A-C]{3}'/></xs:restriction></xs:simpleType>"
				+ "<xs:simpleType name='Either'><xs:restriction base='xs:string'><xs:pattern value='a+'/><xs:pattern value='b+'/></xs:restriction></xs:simpleType>"
				+ "<xs:simpleType name='Chain'><xs:restriction base='tns:Letters'/></xs:simpleType>"
				+ "<xs:simpleType name='ColorCode'><xs:restriction base='tns:Color'><xs:pattern value='[A-Z].*'/></xs:restriction></xs:simpleType>"
				+ "<xs:simpleType name='NumCode'><xs:restriction><xs:simpleType><xs:restriction base='xs:int'/></xs:simpleType><xs:pattern value='[0-9]+'/></xs:restriction></xs:simpleType>";
			var template = TemplateOf(
				OpElement(
					"<xs:element name='Color' type='tns:Color'/><xs:element name='Code' type='tns:Code'/><xs:element name='Wide' type='tns:Wide'/><xs:element name='Narrow' type='tns:Narrow'/>"
					+ "<xs:element name='Letters' type='tns:Letters'/><xs:element name='Either' type='tns:Either'/><xs:element name='Chain' type='tns:Chain'/><xs:element name='ColorCode' type='tns:ColorCode'/><xs:element name='NumCode' type='tns:NumCode'/>"
					+ "<xs:element name='Inline'><xs:simpleType><xs:restriction base='xs:int'><xs:enumeration value='1'/><xs:enumeration value='2'/></xs:restriction></xs:simpleType></xs:element>"
					+ "<xs:element name='NoBase'><xs:simpleType><xs:restriction><xs:enumeration value='x'/></xs:restriction></xs:simpleType></xs:element>"),
				extras);

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Color simple string{Red|Green|Blue} [1..1]",
					"  Code simple string /[A-Z]{3}/ [1..1]",
					"  Wide simple long{1|2} [1..1]",
					"  Narrow simple long{2} [1..1]",
					"  Letters simple string /[A-C]{3}/ [1..1]",
					"  Either simple string /(?:a+)|(?:b+)/ [1..1]",
					"  Chain simple string /[A-C]{3}/ [1..1]",
					"  ColorCode simple string{Red|Green|Blue} /[A-Z].*/ [1..1]",
					"  NumCode simple int /[0-9]+/ [1..1]",
					"  Inline simple int{1|2} [1..1]",
					"  NoBase simple string{x} [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_TheEnumerationValuesKeepTheirOrderAndTheirText()
		{
			var template = TemplateOf(
				OpElement("<xs:element name='V' type='tns:Odd'/>"),
				"<xs:simpleType name='Odd'><xs:restriction base='xs:string'><xs:enumeration value='b'/><xs:enumeration value='a'/><xs:enumeration value=' padded '/><xs:enumeration value=''/><xs:enumeration value='&lt;x&gt;&amp;'/><xs:enumeration value='a'/></xs:restriction></xs:simpleType>");

			var values = Strings(template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children")[0].GetProperty("type").GetProperty("enumValues"));

			CollectionAssert.AreEqual(new[] { "b", "a", " padded ", string.Empty, "<x>&", "a" }, values);
		}

		[TestMethod]
		public void BuildFormTemplate_AnEnumerationOfTwoHundredValuesIsKept()
		{
			var template = TemplateOf(OpElement("<xs:element name='V' type='tns:Many'/>"), ManyValuesType(200));

			var node = template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children")[0];

			Assert.AreEqual(200, node.GetProperty("type").GetProperty("enumValues").GetArrayLength());
			Assert.AreEqual("v199", node.GetProperty("type").GetProperty("enumValues")[199].GetString());
			Assert.AreEqual(string.Empty, node.GetProperty("note").GetString());
		}

		[TestMethod]
		public void BuildFormTemplate_AnEnumerationOfMoreThanTwoHundredValuesIsNotOffered()
		{
			var template = TemplateOf(OpElement("<xs:element name='V' type='tns:Many'/>"), ManyValuesType(201));

			var node = template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children")[0];

			Assert.AreEqual("simple", node.GetProperty("kind").GetString());
			Assert.AreEqual("string", node.GetProperty("type").GetProperty("xsd").GetString());
			Assert.AreEqual(JsonValueKind.Null, node.GetProperty("type").GetProperty("enumValues").ValueKind);
			Assert.AreEqual("enumeration with more than 200 values", node.GetProperty("note").GetString());
			Assert.IsFalse(node.GetProperty("anyContent").GetBoolean());
		}

		[TestMethod]
		public void BuildFormTemplate_SpecialTypesFixedValuesAndUnsupportedConstructs()
		{
			var template = TemplateOf(OpElement(
				"<xs:element name='Id' type='ser:guid'/>"
				+ "<xs:element name='Ch' type='ser:char'/>"
				+ "<xs:element name='Dur' type='ser:duration'/>"
				+ "<xs:element name='Any' type='xs:anyType'/>"
				+ "<xs:element name='NoType'/>"
				+ "<xs:element name='WhenDateTimeOffset' type='xs:string'/>"
				+ "<xs:element name='NotDateTimeOffset' type='xs:int'/>"
				+ "<xs:element name='FixedDateTimeOffset' type='xs:string' fixed='x'/>"
				+ "<xs:element name='Fixed' type='xs:string' fixed='F1'/>"
				+ "<xs:element name='Default' type='xs:int' default='7'/>"
				+ "<xs:element name='Both' type='xs:int' fixed='1' default='2'/>"
				+ "<xs:element name='InlineFixed' fixed='9'><xs:simpleType><xs:restriction base='xs:int'/></xs:simpleType></xs:element>"
				+ "<xs:element name='Lst'><xs:simpleType><xs:list itemType='xs:int'/></xs:simpleType></xs:element>"
				+ "<xs:element name='Uni'><xs:simpleType><xs:union memberTypes='xs:int xs:string'/></xs:simpleType></xs:element>"
				+ "<xs:element name='Lost' type='tns:NoSuchType'/>"));

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Id simple guid [1..1]",
					"  Ch simple char [1..1]",
					"  Dur simple duration [1..1]",
					"  Any simple string [1..1] any note=\"anyType: any content is allowed\"",
					"  NoType simple string [1..1]",
					"  WhenDateTimeOffset simple dateTime [1..1] note=\"ISO 8601 date-time with offset\"",
					"  NotDateTimeOffset simple int [1..1]",
					"  FixedDateTimeOffset simple string [1..1] note=\"fixed: x\"",
					"  Fixed simple string [1..1] note=\"fixed: F1\"",
					"  Default simple int [1..1] note=\"default: 7\"",
					"  Both simple int [1..1] note=\"fixed: 1\"",
					"  InlineFixed simple int [1..1] note=\"fixed: 9\"",
					"  Lst simple string [1..1] any note=\"unsupported schema construct: simpleType/list\"",
					"  Uni simple string [1..1] any note=\"unsupported schema construct: simpleType/union\"",
					"  Lost complex [1..1] any note=\"unresolved type: NoSuchType\""),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_TheNoteOfAFixedValueStartsWithFixedAndKeepsTheValueAsItIs()
		{
			var template = TemplateOf(OpElement("<xs:element name='A' type='xs:string' fixed='  a; b  '/><xs:element name='B' type='xs:string' default=''/>"));

			var notes = template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children").EnumerateArray().Select(node => node.GetProperty("note").GetString()).ToArray();

			CollectionAssert.AreEqual(new[] { "fixed:   a; b  ", "default: " }, notes);
		}

		[TestMethod]
		public void BuildFormTemplate_DataContractArraysAndSerializationElementsThatTheWsdlDoesNotDeclare()
		{
			var template = TemplateOf(OpElement(
				"<xs:element name='Arr' type='arr:ArrayOfint'/>"
				+ "<xs:element name='Arr2' type='arr:ArrayOfguid'/>"
				+ "<xs:element name='Arr3' type='arr:ArrayOfdateTime'/>"
				+ "<xs:element name='Arr4' type='arr:ArrayOfSomething'/>"
				+ "<xs:element name='NotArray' type='arr:Plain'/>"
				+ "<xs:element ref='ser:string'/>"
				+ "<xs:element ref='ser:guid' minOccurs='0'/>"
				+ "<xs:element ref='ser:unknownThing'/>"
				+ "<xs:element ref='tns:Missing' minOccurs='0' maxOccurs='unbounded'/>"));
			const string Arrays = "{http://schemas.microsoft.com/2003/10/Serialization/Arrays}";
			const string Serialization = "{http://schemas.microsoft.com/2003/10/Serialization/}";

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Arr complex [1..1]",
					"    " + Arrays + "int simple int [0..*]",
					"  Arr2 complex [1..1]",
					"    " + Arrays + "guid simple guid [0..*]",
					"  Arr3 complex [1..1]",
					"    " + Arrays + "dateTime simple dateTime [0..*]",
					"  Arr4 complex [1..1]",
					"    " + Arrays + "Something simple string [0..*]",
					"  NotArray simple string [1..1]",
					"  " + Serialization + "string simple string [1..1]",
					"  " + Serialization + "guid simple guid [0..1]",
					"  " + Serialization + "unknownThing simple string [1..1]",
					"  Missing complex [0..*] any note=\"unresolved element reference: Missing\""),
				Render(template));
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: attributes
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildFormTemplate_AttributesWithTheirTypeRequirementAndFixedValue()
		{
			var attributes = "<xs:attribute name='id' type='xs:int' use='required'/>"
				+ "<xs:attribute name='kind' type='tns:Color' default='Red'/>"
				+ "<xs:attribute name='version' type='xs:string' fixed='1.0'/>"
				+ "<xs:attribute name='off' type='xs:string' use='prohibited'/>"
				+ "<xs:attribute ref='xml:lang'/>"
				+ "<xs:attribute ref='ser:Id'/>"
				+ "<xs:attribute ref='tns:gattr' use='required'/>"
				+ "<xs:attribute ref='tns:nowhere'/>"
				+ "<xs:attribute name='loose'><xs:simpleType><xs:restriction base='xs:string'><xs:pattern value='[a-z]+'/></xs:restriction></xs:simpleType></xs:attribute>"
				+ "<xs:attribute name='xmlns' type='xs:string'/>"
				+ "<xs:attribute name='xmlns:evil' type='xs:string'/>"
				+ "<xs:attribute name='bad name' type='xs:string'/>"
				+ "<xs:attribute name='qual' type='xs:string' form='qualified'/>"
				+ "<xs:attribute name='untyped'/>";
			var template = TemplateOf(OpElement("<xs:element name='A' type='xs:string'/>", attributes), ColorType + "<xs:attribute name='gattr' type='xs:int' fixed='5'/>");

			Assert.AreEqual(
				Join(
					"Op complex [1..1] note=\"unresolved attribute reference: nowhere\"",
					"  @id int required",
					"  @kind string{Red|Green|Blue}",
					"  @version string fixed=1.0",
					"  @{http://www.w3.org/XML/1998/namespace}lang string",
					"  @{urn:t}gattr int required fixed=5",
					"  @loose string /[a-z]+/",
					"  @{urn:t}qual string",
					"  @untyped string",
					"  A simple string [1..1]"),
				Render(template));
			CollectionAssert.AreEqual(new[] { "An attribute was skipped, the name is not valid XML: bad name" }, Warnings(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AttributesAreUnqualifiedUnlessTheSchemaSaysOtherwise()
		{
			var types = "<xs:schema elementFormDefault='qualified' attributeFormDefault='qualified' targetNamespace='urn:t'>"
				+ OpElement("<xs:element name='A' type='xs:string'/>", "<xs:attribute name='q' type='xs:string'/><xs:attribute name='u' type='xs:string' form='unqualified'/>")
				+ "</xs:schema>";

			var template = Template(WsdlTypes(types, "Op"), "Op");

			Assert.AreEqual(Join("Op complex [1..1]", "  @{urn:t}q string", "  @u string", "  A simple string [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_TheAttributesOfAnElementWithASimpleTypeAreNotListed()
		{
			var template = TemplateOf(OpElement("<xs:element name='A' type='xs:string'/>"));

			Assert.AreEqual(0, template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children")[0].GetProperty("attributes").GetArrayLength());
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: extensions and constructs the form cannot edit
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildFormTemplate_ABaseTypeComesFirstAndItsAttributesAreKept()
		{
			var extras = "<xs:complexType name='Base'><xs:sequence><xs:element name='BaseField' type='xs:string'/></xs:sequence><xs:attribute name='baseAttr' type='xs:string'/></xs:complexType>"
				+ "<xs:complexType name='Derived'><xs:complexContent><xs:extension base='tns:Base'><xs:sequence><xs:element name='DerivedField' type='xs:int'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='Leaf'><xs:complexContent><xs:extension base='tns:Derived'><xs:sequence><xs:element name='LeafField' type='xs:boolean'/></xs:sequence><xs:attribute name='leafAttr' type='xs:int'/></xs:extension></xs:complexContent></xs:complexType>";

			var template = TemplateOf(OpElement("<xs:element name='Leaf' type='tns:Leaf'/>"), extras);

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Leaf complex [1..1]",
					"    @baseAttr string",
					"    @leafAttr int",
					"    BaseField simple string [1..1]",
					"    DerivedField simple int [1..1]",
					"    LeafField simple boolean [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AnExtensionThatCannotBeExpandedSaysSo()
		{
			var extras = "<xs:complexType name='Base'><xs:sequence><xs:element name='BaseField' type='xs:string'/></xs:sequence></xs:complexType>"
				+ "<xs:complexType name='Cyc1'><xs:complexContent><xs:extension base='tns:Cyc2'><xs:sequence><xs:element name='C1' type='xs:string'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='Cyc2'><xs:complexContent><xs:extension base='tns:Cyc1'><xs:sequence><xs:element name='C2' type='xs:string'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='Orphan'><xs:complexContent><xs:extension base='tns:Nowhere'><xs:sequence><xs:element name='O' type='xs:string'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='Narrowed'><xs:complexContent><xs:restriction base='tns:Base'><xs:sequence><xs:element name='BaseField' type='xs:string'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='OfAnyType'><xs:complexContent><xs:extension base='xs:anyType'><xs:sequence><xs:element name='P' type='xs:string'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='CycA'><xs:complexContent><xs:extension base='tns:CycB'><xs:sequence><xs:element name='A' type='xs:string'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='CycB'><xs:complexContent><xs:extension base='tns:CycA'><xs:sequence><xs:element name='B' type='xs:string'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>"
				+ "<xs:complexType name='Entry'><xs:complexContent><xs:extension base='tns:CycA'><xs:sequence><xs:element name='E' type='xs:string'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>";

			var template = TemplateOf(
				OpElement("<xs:element name='Cyc' type='tns:Cyc1'/><xs:element name='Orphan' type='tns:Orphan'/><xs:element name='Narrowed' type='tns:Narrowed'/><xs:element name='OfAnyType' type='tns:OfAnyType'/><xs:element name='Entry' type='tns:Entry'/>"),
				extras);

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Cyc complex [1..1] truncated note=\"recursive base type: Cyc1\"",
					"    C2 simple string [1..1]",
					"    C1 simple string [1..1]",
					"  Orphan complex [1..1] any note=\"unresolved base type: Nowhere\"",
					"    O simple string [1..1]",
					"  Narrowed complex [1..1] any note=\"unsupported schema construct: complexContent/restriction\"",
					"  OfAnyType complex [1..1]",
					"    P simple string [1..1]",
					"  Entry complex [1..1] truncated note=\"recursive base type: CycA\"",
					"    B simple string [1..1]",
					"    A simple string [1..1]",
					"    E simple string [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_SimpleContentGroupsAndAttributeGroupsAreFlagged()
		{
			var extras = "<xs:complexType name='Text'><xs:simpleContent><xs:extension base='xs:string'><xs:attribute name='u' type='xs:string'/></xs:extension></xs:simpleContent></xs:complexType>"
				+ "<xs:complexType name='Grouped'><xs:group ref='tns:G'/></xs:complexType>"
				+ "<xs:complexType name='WithAttrGroup'><xs:sequence><xs:element name='W' type='xs:string'/></xs:sequence><xs:attributeGroup ref='tns:AG'/></xs:complexType>";

			var template = TemplateOf(OpElement("<xs:element name='Text' type='tns:Text'/><xs:element name='Grouped' type='tns:Grouped'/><xs:element name='WithAttrGroup' type='tns:WithAttrGroup'/>"), extras);

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Text complex [1..1] any note=\"unsupported schema construct: simpleContent\"",
					"  Grouped complex [1..1] any note=\"unsupported schema construct: group\"",
					"  WithAttrGroup complex [1..1] any note=\"unsupported schema construct: attributeGroup\"",
					"    W simple string [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_InlineComplexTypesAndEmptyTypesWork()
		{
			var template = TemplateOf(OpElement(
				"<xs:element name='Inline'><xs:complexType><xs:sequence><xs:element name='X' type='xs:int'/></xs:sequence><xs:attribute name='a' type='xs:int'/></xs:complexType></xs:element>"
				+ "<xs:element name='Empty'><xs:complexType><xs:sequence/></xs:complexType></xs:element>"
				+ "<xs:element name='Bare'><xs:complexType/></xs:element>"));

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Inline complex [1..1]",
					"    @a int",
					"    X simple int [1..1]",
					"  Empty complex [1..1]",
					"  Bare complex [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AnOperationWithoutParametersHasAnEmptyBodyPart()
		{
			var template = TemplateOf("<xs:element name='Op'><xs:complexType><xs:sequence/></xs:complexType></xs:element>");

			Assert.AreEqual("Op complex [1..1]", Render(template));
			Assert.AreEqual(0, template.GetProperty("template").GetProperty("bodyParts")[0].GetProperty("children").GetArrayLength());
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: choices, groups and wildcards
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildFormTemplate_TheAlternativesOfAChoiceShareOneChoiceGroup()
		{
			var template = TemplateOf(OpElement(
				"<xs:choice><xs:element name='A' type='xs:string'/><xs:element name='B' type='xs:int'/></xs:choice>"
				+ "<xs:element name='Between' type='xs:string'/>"
				+ "<xs:choice minOccurs='0'><xs:element name='C' type='xs:string'/><xs:element name='D' type='xs:string'/></xs:choice>"
				+ "<xs:choice><xs:element name='E' type='xs:string'/><xs:element name='F' type='xs:string'/><xs:element name='G' type='xs:string'/></xs:choice>"));

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  A simple string [1..1] group=c1",
					"  B simple int [1..1] group=c1",
					"  Between simple string [1..1]",
					"  C simple string [0..1] group=c2",
					"  D simple string [0..1] group=c2",
					"  E simple string [1..1] group=c3",
					"  F simple string [1..1] group=c3",
					"  G simple string [1..1] group=c3"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ChoiceGroupsAreUniqueAcrossTheWholeTemplateEvenInNestedTypes()
		{
			var extras = "<xs:complexType name='Inner'><xs:choice><xs:element name='I1' type='xs:string'/><xs:element name='I2' type='xs:string'/></xs:choice></xs:complexType>";

			var template = TemplateOf(
				OpElement(
					"<xs:choice><xs:element name='A' type='xs:string'/><xs:element name='B' type='xs:string'/></xs:choice>"
					+ "<xs:element name='X' type='tns:Inner'/>"
					+ "<xs:element name='Y' type='tns:Inner'/>"
					+ "<xs:choice><xs:element name='C' type='xs:string'/><xs:element name='D' type='xs:string'/></xs:choice>"),
				extras);

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  A simple string [1..1] group=c1",
					"  B simple string [1..1] group=c1",
					"  X complex [1..1]",
					"    I1 simple string [1..1] group=c2",
					"    I2 simple string [1..1] group=c2",
					"  Y complex [1..1]",
					"    I1 simple string [1..1] group=c3",
					"    I2 simple string [1..1] group=c3",
					"  C simple string [1..1] group=c4",
					"  D simple string [1..1] group=c4"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_EveryAlternativeKeepsItsOwnOccurrence()
		{
			var template = TemplateOf(OpElement(
				"<xs:choice><xs:element name='A' type='xs:string' minOccurs='0'/><xs:element name='B' type='xs:string' maxOccurs='unbounded'/><xs:element name='C' type='xs:string' minOccurs='2' maxOccurs='4'/></xs:choice>"));

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  A simple string [0..1] group=c1",
					"  B simple string [1..*] group=c1",
					"  C simple string [2..4] group=c1"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AChoiceWithOneAlternativeIsAPlainElement()
		{
			var template = TemplateOf(OpElement("<xs:choice><xs:element name='Only' type='xs:string'/></xs:choice>"));

			Assert.AreEqual(Join("Op complex [1..1]", "  Only simple string [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AnAlternativeThatIsNotASingleElementFlagsTheParentAndMakesTheElementsOptional()
		{
			var single = TemplateOf(OpElement("<xs:choice><xs:element name='G' type='xs:string'/><xs:sequence><xs:element name='H1' type='xs:string'/><xs:element name='H2' type='xs:string'/></xs:sequence></xs:choice>"));
			var several = TemplateOf(OpElement("<xs:choice><xs:element name='A' type='xs:string'/><xs:element name='B' type='xs:string'/><xs:sequence><xs:element name='H' type='xs:string'/></xs:sequence></xs:choice>"));

			Assert.AreEqual(
				Join("Op complex [1..1] any note=\"choice alternative that is not a single element: sequence\"", "  G simple string [0..1]"),
				Render(single));
			Assert.AreEqual(
				Join("Op complex [1..1] any note=\"choice alternative that is not a single element: sequence\"", "  A simple string [0..1] group=c1", "  B simple string [0..1] group=c1"),
				Render(several));
		}

		[TestMethod]
		public void BuildFormTemplate_ARepeatingChoiceFlagsTheParentAndLetsItsElementsRepeat()
		{
			var template = TemplateOf(OpElement("<xs:choice maxOccurs='unbounded'><xs:element name='E' type='xs:string'/><xs:element name='F' type='xs:string'/></xs:choice>"));

			Assert.AreEqual(
				Join("Op complex [1..1] any note=\"repeating choice\"", "  E simple string [1..*] group=c1", "  F simple string [1..*] group=c1"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AnOptionalSequenceMakesItsElementsOptional()
		{
			var template = TemplateOf(OpElement(
				"<xs:sequence minOccurs='0'><xs:element name='OptA' type='xs:string'/><xs:element name='OptB' type='xs:string' minOccurs='3' maxOccurs='9'/></xs:sequence><xs:element name='After' type='xs:string'/>"));

			Assert.AreEqual(
				Join("Op complex [1..1]", "  OptA simple string [0..1]", "  OptB simple string [0..9]", "  After simple string [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ARepeatingSequenceFlagsTheParentAndLetsItsElementsRepeat()
		{
			var template = TemplateOf(OpElement("<xs:sequence maxOccurs='unbounded'><xs:element name='Rep' type='xs:string'/></xs:sequence><xs:sequence maxOccurs='2'><xs:element name='Twice' type='xs:string'/></xs:sequence>"));

			Assert.AreEqual(
				Join("Op complex [1..1] any note=\"repeating sequence\"", "  Rep simple string [1..*]", "  Twice simple string [1..*]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AParticleThatCannotOccurIsLeftOut()
		{
			var template = TemplateOf(OpElement("<xs:sequence maxOccurs='0'><xs:element name='Gone' type='xs:string'/></xs:sequence><xs:element name='Kept' type='xs:string'/>"));

			Assert.AreEqual(Join("Op complex [1..1]", "  Kept simple string [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AllIsListedLikeASequence()
		{
			var template = TemplateOf("<xs:element name='Op'><xs:complexType><xs:all><xs:element name='X' type='xs:string' minOccurs='0'/><xs:element name='Y' type='xs:int'/></xs:all></xs:complexType></xs:element>");

			Assert.AreEqual(Join("Op complex [1..1]", "  X simple string [0..1]", "  Y simple int [1..1]"), Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_NestedSequencesAreFlattenedInOrder()
		{
			var template = TemplateOf(OpElement(
				"<xs:element name='A' type='xs:string'/><xs:sequence><xs:element name='B' type='xs:string'/><xs:sequence><xs:element name='C' type='xs:string'/></xs:sequence><xs:element name='D' type='xs:string'/></xs:sequence><xs:element name='E' type='xs:string'/>"));

			Assert.AreEqual(
				Join("Op complex [1..1]", "  A simple string [1..1]", "  B simple string [1..1]", "  C simple string [1..1]", "  D simple string [1..1]", "  E simple string [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_AWildcardAndAGroupReferenceFlagTheParent()
		{
			var wildcard = TemplateOf(OpElement("<xs:element name='A' type='xs:string'/><xs:any minOccurs='0' maxOccurs='unbounded'/>"));
			var group = TemplateOf(OpElement("<xs:element name='A' type='xs:string'/><xs:group ref='tns:G'/>"));

			Assert.AreEqual(Join("Op complex [1..1] any note=\"any element is allowed here\"", "  A simple string [1..1]"), Render(wildcard));
			Assert.AreEqual(Join("Op complex [1..1] any note=\"unsupported schema construct: group\"", "  A simple string [1..1]"), Render(group));
		}

		[TestMethod]
		public void BuildFormTemplate_AnnotationsAreIgnored()
		{
			var template = TemplateOf(OpElement("<xs:annotation><xs:documentation>text</xs:documentation></xs:annotation><xs:element name='A' type='xs:string'><xs:annotation><xs:documentation>text</xs:documentation></xs:annotation></xs:element>"));

			Assert.AreEqual(Join("Op complex [1..1]", "  A simple string [1..1]"), Render(template));
		}

		// ------------------------------------------------------------------
		// buildFormTemplate: recursion and limits
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildFormTemplate_ARecursiveTypeIsCutAfterOneLevel()
		{
			var template = TemplateOf(
				OpElement("<xs:element name='Self' type='tns:Loop'/>"),
				"<xs:complexType name='Loop'><xs:sequence><xs:element name='Next' type='tns:Loop' minOccurs='0'/></xs:sequence></xs:complexType>");

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  Self complex [1..1]",
					"    Next complex [0..1] truncated note=\"recursive type Loop, not expanded further\""),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ARecursiveElementReferenceIsCutAfterOneLevel()
		{
			var template = TemplateOf("<xs:element name='Op'><xs:complexType><xs:sequence><xs:element ref='tns:Op' minOccurs='0'/></xs:sequence></xs:complexType></xs:element>");

			Assert.AreEqual(
				Join("Op complex [1..1]", "  Op complex [0..1] truncated note=\"recursive element Op, not expanded further\""),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ElementsThatReferToEachOtherAreCut()
		{
			var template = TemplateOf(
				OpElement("<xs:element ref='tns:RefA'/>"),
				"<xs:element name='RefA'><xs:complexType><xs:sequence><xs:element ref='tns:RefB'/></xs:sequence></xs:complexType></xs:element><xs:element name='RefB'><xs:complexType><xs:sequence><xs:element ref='tns:RefA'/></xs:sequence></xs:complexType></xs:element>");

			Assert.AreEqual(
				Join(
					"Op complex [1..1]",
					"  RefA complex [1..1]",
					"    RefB complex [1..1]",
					"      RefA complex [1..1] truncated note=\"recursive element RefA, not expanded further\""),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ARepeatedTypeThatIsNotRecursiveIsExpandedEveryTime()
		{
			var template = TemplateOf(
				OpElement("<xs:element name='X' type='tns:Pair'/><xs:element name='Y' type='tns:Pair'/>"),
				"<xs:complexType name='Pair'><xs:sequence><xs:element name='Left' type='xs:string'/></xs:sequence></xs:complexType>");

			Assert.AreEqual(
				Join("Op complex [1..1]", "  X complex [1..1]", "    Left simple string [1..1]", "  Y complex [1..1]", "    Left simple string [1..1]"),
				Render(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ElementsDeeperThanTenLevelsAreNotExpanded()
		{
			var template = TemplateOf(NestedElements(12));

			var lines = Render(template).Split('\n');

			// The body part is level 1, so the tenth level is the last one that is built.
			Assert.AreEqual(10, lines.Length, Render(template));
			StringAssert.StartsWith(lines[9], new string(' ', 18) + "N10 complex [1..1] truncated note=\"maximum depth reached, deeper elements are not shown\"");
			CollectionAssert.AreEqual(new[] { "The form stops expanding at depth 10." }, Warnings(template));
		}

		[TestMethod]
		public void BuildFormTemplate_TenLevelsAreNotTruncated()
		{
			var template = TemplateOf(NestedElements(10));

			var lines = Render(template).Split('\n');

			Assert.AreEqual(10, lines.Length);
			Assert.IsFalse(lines.Any(line => line.Contains("truncated", StringComparison.Ordinal)), Render(template));
			Assert.AreEqual(0, Warnings(template).Length);
		}

		[TestMethod]
		public void BuildFormTemplate_FiftyNestedParticlesAreTheLimit()
		{
			var within = TemplateOf(OpElement(NestedSequences(49, "<xs:element name='Deep' type='xs:string'/>")));
			var beyond = TemplateOf(OpElement(NestedSequences(50, "<xs:element name='Deep' type='xs:string'/>")));

			Assert.AreEqual(Join("Op complex [1..1]", "  Deep simple string [1..1]"), Render(within));
			Assert.AreEqual("Op complex [1..1] truncated note=\"schema nesting limit reached\"", Render(beyond));
		}

		[TestMethod]
		public void BuildFormTemplate_TheNumberOfNodesIsLimitedToTwoThousand()
		{
			// 5 elements of 5 levels of types make 3906 nodes, the walk stops after the 2000th.
			var template = TemplateOf(FanOut(5, 4));

			var nodes = Descendants(template.GetProperty("template").GetProperty("bodyParts")[0]).ToArray();

			Assert.AreEqual(2000, nodes.Length);
			Assert.IsTrue(nodes.Any(node => node.GetProperty("truncated").GetBoolean()), "A node that was not expanded completely must say so.");
			CollectionAssert.AreEqual(new[] { "The form was truncated because it reached the size limit (2,000 elements)." }, Warnings(template));
		}

		[TestMethod]
		public void BuildFormTemplate_ASchemaBelowTheLimitIsNotTruncated()
		{
			// 6 elements of 3 levels of types make 1555 nodes.
			var template = TemplateOf(FanOut(6, 3));

			var nodes = Descendants(template.GetProperty("template").GetProperty("bodyParts")[0]).ToArray();

			Assert.AreEqual(1555, nodes.Length);
			Assert.IsFalse(nodes.Any(node => node.GetProperty("truncated").GetBoolean()));
			Assert.AreEqual(0, Warnings(template).Length);
		}

		// ------------------------------------------------------------------
		// buildWsdlOutline
		// ------------------------------------------------------------------
		[TestMethod]
		public void BuildWsdlOutline_ListsServicesBindingsPortTypesMessagesAndTypes()
		{
			var result = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(OutlineWsdl) + ")");

			AssertOk(result);
			var outline = result.GetProperty("outline");
			CollectionAssert.AreEquivalent(
				new[] { "targetNamespace", "services", "bindings", "portTypes", "messages", "types", "warnings" },
				outline.EnumerateObject().Select(property => property.Name).ToArray());
			Assert.AreEqual("urn:t", outline.GetProperty("targetNamespace").GetString());
			Assert.AreEqual(0, outline.GetProperty("warnings").GetArrayLength());

			Assert.AreEqual(1, outline.GetProperty("services").GetArrayLength());
			var service = outline.GetProperty("services")[0];
			Assert.AreEqual("S", service.GetProperty("name").GetString());
			CollectionAssert.AreEqual(
				new[] { "P11|B11|https://example.com/S.svc|1.1", "P12|B12|https://example.com/S.svc|1.2", "PNoAddress|B12||1.2", "PUnknown|Missing||" },
				service.GetProperty("ports").EnumerateArray().Select(port => string.Join("|", port.GetProperty("name").GetString(), port.GetProperty("binding").GetString(), port.GetProperty("address").GetString(), port.GetProperty("soapVersion").GetString())).ToArray());

			CollectionAssert.AreEqual(
				new[] { "B11|P|http://schemas.xmlsoap.org/soap/http|document|1.1|Get=urn:t/Get;Set=urn:t/Set", "B12|P|http://schemas.xmlsoap.org/soap/http||1.2|Get=urn:t/Get", "BHttp|P||||" },
				outline.GetProperty("bindings").EnumerateArray().Select(DescribeBinding).ToArray());

			CollectionAssert.AreEqual(
				new[] { "P:Get(GetIn)->GetOut!Fault1,Fault2;Set(SetIn)->!" },
				outline.GetProperty("portTypes").EnumerateArray().Select(DescribePortType).ToArray());

			CollectionAssert.AreEqual(
				new[] { "GetIn:parameters=Get/", "GetOut:parameters=GetResponse/", "RpcIn:a=/string;b=/Item", "Fault1:detail=Fault/" },
				outline.GetProperty("messages").EnumerateArray().Select(DescribeMessage).ToArray());

			var types = outline.GetProperty("types");
			CollectionAssert.AreEqual(new[] { "Get", "GetResponse", "Fault" }, Strings(types.GetProperty("elements")));
			CollectionAssert.AreEqual(new[] { "Item", "ArrayOfItem" }, Strings(types.GetProperty("complexTypes")));
			CollectionAssert.AreEqual(
				new[] { "Color=3", "Flags=1", "Code=0" },
				types.GetProperty("simpleTypes").EnumerateArray().Select(type => type.GetProperty("name").GetString() + "=" + type.GetProperty("enumCount").GetInt32()).ToArray());
		}

		[TestMethod]
		public void BuildWsdlOutline_ReferencesAreLocalNamesWithoutPrefix()
		{
			var result = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(OutlineWsdl) + ")");

			var outline = result.GetProperty("outline");
			var portType = outline.GetProperty("portTypes")[0];
			Assert.AreEqual("GetIn", portType.GetProperty("operations")[0].GetProperty("input").GetString());
			Assert.AreEqual("P", outline.GetProperty("bindings")[0].GetProperty("type").GetString());
			Assert.AreEqual("B11", outline.GetProperty("services")[0].GetProperty("ports")[0].GetProperty("binding").GetString());
			Assert.AreEqual("Get", outline.GetProperty("messages")[0].GetProperty("parts")[0].GetProperty("element").GetString());
			Assert.AreEqual("string", outline.GetProperty("messages")[2].GetProperty("parts")[0].GetProperty("type").GetString());
			Assert.AreEqual("Item", outline.GetProperty("messages")[2].GetProperty("parts")[1].GetProperty("type").GetString());
		}

		[TestMethod]
		public void BuildWsdlOutline_TheSoapVersionComesFromTheBindingExtensionNamespace()
		{
			var result = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(OutlineWsdl) + ")");

			var bindings = result.GetProperty("outline").GetProperty("bindings").EnumerateArray().ToArray();

			Assert.AreEqual("1.1", bindings[0].GetProperty("soapVersion").GetString());
			Assert.AreEqual("1.2", bindings[1].GetProperty("soapVersion").GetString());
			Assert.AreEqual(string.Empty, bindings[2].GetProperty("soapVersion").GetString());
		}

		[TestMethod]
		public void BuildWsdlOutline_APortWithoutAnAddressTakesTheVersionOfItsBindingEvenWhenTheServiceComesFirst()
		{
			var wsdl = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' xmlns:soap12='http://schemas.xmlsoap.org/wsdl/soap12/' xmlns:tns='urn:t'>"
				+ "<wsdl:service name='S'><wsdl:port name='P' binding='tns:B'/></wsdl:service>"
				+ "<wsdl:binding name='B' type='tns:T'><soap12:binding transport='x'/></wsdl:binding>"
				+ "</wsdl:definitions>";

			var port = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(wsdl) + ")").GetProperty("outline").GetProperty("services")[0].GetProperty("ports")[0];

			Assert.AreEqual("1.2", port.GetProperty("soapVersion").GetString());
			Assert.AreEqual(string.Empty, port.GetProperty("address").GetString());
		}

		[TestMethod]
		public void BuildWsdlOutline_AWsdlWithoutServicesIsFine()
		{
			var wsdl = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' targetNamespace='urn:x'><wsdl:message name='m'/></wsdl:definitions>";

			var result = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(wsdl) + ")");

			AssertOk(result);
			var outline = result.GetProperty("outline");
			Assert.AreEqual(0, outline.GetProperty("services").GetArrayLength());
			Assert.AreEqual(0, outline.GetProperty("bindings").GetArrayLength());
			Assert.AreEqual(1, outline.GetProperty("messages").GetArrayLength());
			Assert.AreEqual(0, outline.GetProperty("messages")[0].GetProperty("parts").GetArrayLength());
		}

		[TestMethod]
		public void BuildWsdlOutline_AnEmptyDefinitionsElementGivesAnEmptyOutline()
		{
			var result = WithDom("window.SoapUiWsdl.buildWsdlOutline('<definitions xmlns=\"http://schemas.xmlsoap.org/wsdl/\"/>')");

			AssertOk(result);
			var outline = result.GetProperty("outline");
			Assert.AreEqual(string.Empty, outline.GetProperty("targetNamespace").GetString());
			Assert.AreEqual(0, outline.GetProperty("types").GetProperty("elements").GetArrayLength());
		}

		[TestMethod]
		public void BuildWsdlOutline_AWsdlThatUsesDefaultNamespacesAndOtherPrefixesIsRead()
		{
			var wsdl = "<definitions xmlns='http://schemas.xmlsoap.org/wsdl/' xmlns:s='http://www.w3.org/2001/XMLSchema' xmlns:b='http://schemas.xmlsoap.org/wsdl/soap/' targetNamespace='urn:d'>"
				+ "<types><s:schema targetNamespace='urn:d'><s:element name='E'/><s:simpleType name='T'><s:restriction base='s:string'><s:enumeration value='a'/><s:enumeration value='b'/></s:restriction></s:simpleType></s:schema></types>"
				+ "<portType name='P'><operation name='O'><input message='x:In'/></operation></portType>"
				+ "<binding name='B' type='y:P'><b:binding style='rpc' transport='t'/><operation name='O'><b:operation soapAction='act'/></operation></binding>"
				+ "</definitions>";

			var outline = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(wsdl) + ")").GetProperty("outline");

			Assert.AreEqual("E", Strings(outline.GetProperty("types").GetProperty("elements"))[0]);
			Assert.AreEqual(2, outline.GetProperty("types").GetProperty("simpleTypes")[0].GetProperty("enumCount").GetInt32());
			Assert.AreEqual("In", outline.GetProperty("portTypes")[0].GetProperty("operations")[0].GetProperty("input").GetString());
			var binding = outline.GetProperty("bindings")[0];
			Assert.AreEqual("P", binding.GetProperty("type").GetString());
			Assert.AreEqual("rpc", binding.GetProperty("style").GetString());
			Assert.AreEqual("t", binding.GetProperty("transport").GetString());
			Assert.AreEqual("act", binding.GetProperty("operations")[0].GetProperty("soapAction").GetString());
		}

		[TestMethod]
		public void BuildWsdlOutline_TypesFromSeveralSchemaBlocksAreAllListed()
		{
			var types = "<xs:schema targetNamespace='urn:a'><xs:element name='A1'/><xs:complexType name='CA'/></xs:schema>"
				+ "<xs:schema targetNamespace='urn:b'><xs:element name='B1'/><xs:element name='B2'/><xs:complexType name='CB'/><xs:simpleType name='SB'><xs:list><xs:simpleType><xs:restriction base='xs:string'><xs:enumeration value='x'/><xs:enumeration value='y'/></xs:restriction></xs:simpleType></xs:list></xs:simpleType></xs:schema>"
				+ "<xs:schema targetNamespace='urn:c'><xs:simpleType name='SC'><xs:union memberTypes='xs:int'/></xs:simpleType><xs:attribute name='ignored'/><xs:element/></xs:schema>";

			var outline = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(WsdlTypes(types)) + ")").GetProperty("outline");

			CollectionAssert.AreEqual(new[] { "A1", "B1", "B2" }, Strings(outline.GetProperty("types").GetProperty("elements")));
			CollectionAssert.AreEqual(new[] { "CA", "CB" }, Strings(outline.GetProperty("types").GetProperty("complexTypes")));
			CollectionAssert.AreEqual(
				new[] { "SB=2", "SC=0" },
				outline.GetProperty("types").GetProperty("simpleTypes").EnumerateArray().Select(type => type.GetProperty("name").GetString() + "=" + type.GetProperty("enumCount").GetInt32()).ToArray());
		}

		[TestMethod]
		public void BuildWsdlOutline_ReferencesByLocationAreWarnedAboutAndOnesWithoutAreNot()
		{
			var withLocation = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' xmlns:xs='http://www.w3.org/2001/XMLSchema'><wsdl:types><xs:schema><xs:import namespace='urn:x' schemaLocation='x.xsd'/></xs:schema></wsdl:types></wsdl:definitions>";
			var wsdlImport = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/'><wsdl:import namespace='urn:x' location='x.wsdl'/></wsdl:definitions>";
			var include = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' xmlns:xs='http://www.w3.org/2001/XMLSchema'><wsdl:types><xs:schema><xs:include schemaLocation='x.xsd'/></xs:schema></wsdl:types></wsdl:definitions>";
			var withoutLocation = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' xmlns:xs='http://www.w3.org/2001/XMLSchema'><wsdl:types><xs:schema><xs:import namespace='http://schemas.microsoft.com/2003/10/Serialization/'/></xs:schema></wsdl:types><wsdl:import namespace='urn:y'/></wsdl:definitions>";

			foreach (var wsdl in new[] { withLocation, wsdlImport, include })
			{
				var warnings = Strings(WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(wsdl) + ")").GetProperty("outline").GetProperty("warnings"));

				Assert.AreEqual(1, warnings.Length, wsdl);
				StringAssert.Contains(warnings[0], "other documents");
			}

			Assert.AreEqual(0, WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(withoutLocation) + ")").GetProperty("outline").GetProperty("warnings").GetArrayLength());
		}

		[TestMethod]
		[DataRow("''")]
		[DataRow("'   '")]
		[DataRow("null")]
		[DataRow("undefined")]
		[DataRow("5")]
		[DataRow("{}")]
		[DataRow("[]")]
		[DataRow("'hello'")]
		[DataRow("'<a/>'")]
		[DataRow("'<a>'")]
		[DataRow("'<a><b></a>'")]
		[DataRow("'<definitions/>'")]
		[DataRow("'<description xmlns=\"http://www.w3.org/ns/wsdl\"/>'")]
		[DataRow("'<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"/>'")]
		[DataRow("'<!DOCTYPE a><a/>'")]
		[DataRow("'<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e \"v\">]><x/>'")]
		[DataRow("'<wsdl:definitions xmlns:wsdl=\"http://schemas.xmlsoap.org/wsdl/\">&e;'")]
		[DataRow("'<!ENTITY e \"v\"><wsdl:definitions xmlns:wsdl=\"http://schemas.xmlsoap.org/wsdl/\"/>'")]
		public void BuildWsdlOutline_FailsForInputThatIsNoWsdl(string textSource)
		{
			var result = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + textSource + ")");

			AssertFailure(result, "outline");
		}

		[TestMethod]
		public void BuildWsdlOutline_ADoctypeIsRejectedWithItsOwnMessage()
		{
			var result = WithDom("window.SoapUiWsdl.buildWsdlOutline('<!DOCTYPE a [<!ENTITY e \"v\">]><wsdl:definitions xmlns:wsdl=\"http://schemas.xmlsoap.org/wsdl/\"/>')");

			Assert.AreEqual("DOCTYPE and ENTITY declarations are not supported.", result.GetProperty("error").GetString());
		}

		[TestMethod]
		public void BuildWsdlOutline_TheListsOfTypesAreCutAtFiveHundredItemsWithAWarning()
		{
			var script = @"(function () {
				var q = String.fromCharCode(39);
				var text = '<wsdl:definitions xmlns:wsdl=' + q + 'http://schemas.xmlsoap.org/wsdl/' + q + ' xmlns:xs=' + q + 'http://www.w3.org/2001/XMLSchema' + q + '><wsdl:types><xs:schema>';
				for (var index = 0; index < 501; index += 1) {
					text += '<xs:element name=' + q + 'e' + index + q + '/><xs:complexType name=' + q + 'c' + index + q + '/><xs:simpleType name=' + q + 's' + index + q + '/>';
				}
				text += '</xs:schema></wsdl:types></wsdl:definitions>';
				var outline = window.SoapUiWsdl.buildWsdlOutline(text).outline;
				return {
					lengths: [outline.types.elements.length, outline.types.complexTypes.length, outline.types.simpleTypes.length],
					warnings: outline.warnings,
					last: outline.types.elements[499]
				};
			}())";

			var result = WithDom(script);

			CollectionAssert.AreEqual(new[] { 500, 500, 500 }, result.GetProperty("lengths").EnumerateArray().Select(length => length.GetInt32()).ToArray());
			Assert.AreEqual("e499", result.GetProperty("last").GetString());
			CollectionAssert.AreEquivalent(
				new[] { "The list of elements was cut at 500 items.", "The list of complex types was cut at 500 items.", "The list of simple types was cut at 500 items." },
				Strings(result.GetProperty("warnings")));
		}

		[TestMethod]
		public void BuildWsdlOutline_TheListsOfMessagesAndOperationsAreCutAtFiveHundredItemsWithAWarning()
		{
			var script = @"(function () {
				var q = String.fromCharCode(39);
				var text = '<wsdl:definitions xmlns:wsdl=' + q + 'http://schemas.xmlsoap.org/wsdl/' + q + '>';
				for (var m = 0; m < 501; m += 1) { text += '<wsdl:message name=' + q + 'm' + m + q + '/>'; }
				text += '<wsdl:portType name=' + q + 'p' + q + '>';
				for (var o = 0; o < 501; o += 1) { text += '<wsdl:operation name=' + q + 'o' + o + q + '/>'; }
				text += '</wsdl:portType><wsdl:binding name=' + q + 'b' + q + '>';
				for (var b = 0; b < 501; b += 1) { text += '<wsdl:operation name=' + q + 'o' + b + q + '/>'; }
				text += '</wsdl:binding></wsdl:definitions>';
				var outline = window.SoapUiWsdl.buildWsdlOutline(text).outline;
				return {
					lengths: [outline.messages.length, outline.portTypes[0].operations.length, outline.bindings[0].operations.length],
					warnings: outline.warnings,
					last: outline.messages[499].name
				};
			}())";

			var result = WithDom(script);

			CollectionAssert.AreEqual(new[] { 500, 500, 500 }, result.GetProperty("lengths").EnumerateArray().Select(length => length.GetInt32()).ToArray());
			Assert.AreEqual("m499", result.GetProperty("last").GetString());
			CollectionAssert.AreEquivalent(
				new[] { "The list of messages was cut at 500 items.", "The list of port type operations was cut at 500 items.", "The list of binding operations was cut at 500 items." },
				Strings(result.GetProperty("warnings")));
		}

		[TestMethod]
		public void BuildWsdlOutline_TheListsOfServicesAndPortsAreCutAtFiveHundredItemsWithAWarning()
		{
			var script = @"(function () {
				var q = String.fromCharCode(39);
				var text = '<wsdl:definitions xmlns:wsdl=' + q + 'http://schemas.xmlsoap.org/wsdl/' + q + '><wsdl:service name=' + q + 's' + q + '>';
				for (var p = 0; p < 501; p += 1) { text += '<wsdl:port name=' + q + 'x' + p + q + '/>'; }
				text += '</wsdl:service>';
				for (var s = 0; s < 500; s += 1) { text += '<wsdl:service name=' + q + 's' + s + q + '/>'; }
				text += '</wsdl:definitions>';
				var outline = window.SoapUiWsdl.buildWsdlOutline(text).outline;
				return {
					lengths: [outline.services.length, outline.services[0].ports.length],
					warnings: outline.warnings,
					last: outline.services[0].ports[499].name
				};
			}())";

			var result = WithDom(script);

			CollectionAssert.AreEqual(new[] { 500, 500 }, result.GetProperty("lengths").EnumerateArray().Select(length => length.GetInt32()).ToArray());
			Assert.AreEqual("x499", result.GetProperty("last").GetString());
			CollectionAssert.AreEquivalent(
				new[] { "The list of services was cut at 500 items.", "The list of ports was cut at 500 items." },
				Strings(result.GetProperty("warnings")));
		}

		[TestMethod]
		public void BuildWsdlOutline_TheListsOfBindingsAndPortTypesAreCutAtFiveHundredItemsWithAWarning()
		{
			var script = @"(function () {
				var q = String.fromCharCode(39);
				var text = '<wsdl:definitions xmlns:wsdl=' + q + 'http://schemas.xmlsoap.org/wsdl/' + q + '>';
				for (var b = 0; b < 501; b += 1) { text += '<wsdl:binding name=' + q + 'b' + b + q + '/>'; }
				for (var t = 0; t < 501; t += 1) { text += '<wsdl:portType name=' + q + 't' + t + q + '/>'; }
				text += '</wsdl:definitions>';
				var outline = window.SoapUiWsdl.buildWsdlOutline(text).outline;
				return {
					lengths: [outline.bindings.length, outline.portTypes.length],
					warnings: outline.warnings,
					last: outline.portTypes[499].name
				};
			}())";

			var result = WithDom(script);

			CollectionAssert.AreEqual(new[] { 500, 500 }, result.GetProperty("lengths").EnumerateArray().Select(length => length.GetInt32()).ToArray());
			Assert.AreEqual("t499", result.GetProperty("last").GetString());
			CollectionAssert.AreEquivalent(
				new[] { "The list of bindings was cut at 500 items.", "The list of port types was cut at 500 items." },
				Strings(result.GetProperty("warnings")));
		}

		[TestMethod]
		public void BuildWsdlOutline_AListOfExactlyFiveHundredItemsIsNotCut()
		{
			var script = @"(function () {
				var q = String.fromCharCode(39);
				var text = '<wsdl:definitions xmlns:wsdl=' + q + 'http://schemas.xmlsoap.org/wsdl/' + q + '>';
				for (var m = 0; m < 500; m += 1) { text += '<wsdl:message name=' + q + 'm' + m + q + '/>'; }
				text += '</wsdl:definitions>';
				var outline = window.SoapUiWsdl.buildWsdlOutline(text).outline;
				return [outline.messages.length, outline.warnings.length];
			}())";

			var result = WithDom(script);

			Assert.AreEqual(500, result[0].GetInt32());
			Assert.AreEqual(0, result[1].GetInt32());
		}

		[TestMethod]
		public void BuildWsdlOutline_TextsAreCutAtThreeHundredCharactersAndHaveNoControlCharacters()
		{
			var longName = new string('n', 400);
			var wsdl = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' targetNamespace='" + longName + "'><wsdl:message name='a&#10;b&#9;c&#13;d'/><wsdl:message name='tab\there'/></wsdl:definitions>";

			var outline = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(wsdl) + ")").GetProperty("outline");

			Assert.AreEqual(300, outline.GetProperty("targetNamespace").GetString().Length);
			Assert.IsTrue(outline.GetProperty("targetNamespace").GetString().EndsWith("...", StringComparison.Ordinal));
			Assert.AreEqual("a b c d", outline.GetProperty("messages")[0].GetProperty("name").GetString());
			Assert.AreEqual("tab here", outline.GetProperty("messages")[1].GetProperty("name").GetString());
		}

		[TestMethod]
		public void BuildWsdlOutline_MarkupInNamesStaysPlainText()
		{
			var wsdl = "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' targetNamespace='&lt;img src=x onerror=alert(1)&gt;'><wsdl:message name='&lt;b&gt;bold&lt;/b&gt;'/></wsdl:definitions>";

			var outline = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(wsdl) + ")").GetProperty("outline");

			Assert.AreEqual("<img src=x onerror=alert(1)>", outline.GetProperty("targetNamespace").GetString());
			Assert.AreEqual("<b>bold</b>", outline.GetProperty("messages")[0].GetProperty("name").GetString());
		}

		[TestMethod]
		public void BuildWsdlOutline_TheOutlineIsPlainDataMadeOfStringsNumbersAndLists()
		{
			var script = @"(function () {
				var outline = window.SoapUiWsdl.buildWsdlOutline(" + Lit(OutlineWsdl) + @").outline;
				var problems = [];
				function walk(value, path) {
					if (typeof value === 'string' || typeof value === 'number') { return; }
					if (Array.isArray(value)) { value.forEach(function (item, index) { walk(item, path + '[' + index + ']'); }); return; }
					if (value !== null && typeof value === 'object' && Object.getPrototypeOf(value) === Object.prototype) { Object.keys(value).forEach(function (key) { walk(value[key], path + '.' + key); }); return; }
					problems.push(path);
				}
				walk(outline, 'outline');
				return problems;
			}())";

			var problems = Strings(WithDom(script));

			Assert.AreEqual(0, problems.Length, "Values that are not plain data: " + string.Join(", ", problems));
		}

		[TestMethod]
		public void BuildWsdlOutline_AWsdlWithOnlyOneServiceHasOneEntryForEachPort()
		{
			var outline = WithDom("window.SoapUiWsdl.buildWsdlOutline(" + Lit(OutlineWsdl) + ")").GetProperty("outline");

			Assert.AreEqual(4, outline.GetProperty("services")[0].GetProperty("ports").GetArrayLength());
		}

		// ------------------------------------------------------------------
		// Helpers
		// ------------------------------------------------------------------
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

		private static string Join(params string[] lines)
		{
			return string.Join("\n", lines);
		}

		// A TypeInfo as JavaScript source.
		private static string TypeSource(string xsd, string enumValuesSource = "null", string patternSource = "null")
		{
			return "{ xsd: " + Lit(xsd) + ", enumValues: " + enumValuesSource + ", pattern: " + patternSource + " }";
		}

		private static string ErrorOf(string xsd, string value)
		{
			return Call("validateXsdValue", TypeSource(xsd), Lit(value)).GetProperty("error").GetString();
		}

		// Adds one to a number written in digits, without a limit on its size.
		private static string IncrementDigits(string digits)
		{
			return (BigInteger.Parse(digits, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
		}

		private static void AssertOk(JsonElement result)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected an object but got: " + result);
			Assert.IsTrue(result.GetProperty("ok").GetBoolean(), "Expected ok but got: " + result);
		}

		private static void AssertFailed(JsonElement result)
		{
			AssertFailure(result, "template");
		}

		private static void AssertFailure(JsonElement result, string payloadProperty)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, "Expected an object but got: " + result);
			Assert.IsFalse(result.GetProperty("ok").GetBoolean(), "Expected a failure but got: " + result);
			Assert.AreEqual(JsonValueKind.String, result.GetProperty("error").ValueKind, "A failure needs an error message: " + result);
			Assert.IsFalse(string.IsNullOrWhiteSpace(result.GetProperty("error").GetString()), "A failure needs an error message: " + result);
			Assert.IsFalse(result.TryGetProperty(payloadProperty, out _), "A failure must not carry any " + payloadProperty + ": " + result);
			CollectionAssert.AreEquivalent(new[] { "ok", "error" }, result.EnumerateObject().Select(property => property.Name).ToArray(), "A failure has only ok and error: " + result);
		}

		private static void AssertValid(JsonElement result, string label)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, label + ": expected an object but got: " + result);
			Assert.IsTrue(result.GetProperty("ok").GetBoolean(), label + ": expected a valid value but got: " + result);
		}

		private static void AssertInvalid(JsonElement result, string label)
		{
			Assert.AreEqual(JsonValueKind.Object, result.ValueKind, label + ": expected an object but got: " + result);
			Assert.IsFalse(result.GetProperty("ok").GetBoolean(), label + ": expected an invalid value but got: " + result);
			Assert.AreEqual(JsonValueKind.String, result.GetProperty("error").ValueKind, label + ": a failure needs an error message: " + result);
			Assert.IsFalse(string.IsNullOrWhiteSpace(result.GetProperty("error").GetString()), label + ": a failure needs an error message: " + result);
		}

		// The tokens as "type|text".
		private static string[] Tokens(string xml)
		{
			return Strings(SoapUiJavaScriptHost.Evaluate("window.SoapUiWsdl.tokenizeXml(" + Lit(xml) + ").tokens.map(function (token) { return token.type + '|' + token.text; })"));
		}

		private static bool TokensAddUp(string textSource)
		{
			var expression = "(function (text) { var result = window.SoapUiWsdl.tokenizeXml(text); return result.ok && result.tokens.map(function (token) { return token.text; }).join('') === text && result.tokens.every(function (token) { return token.text !== ''; }); }(" + textSource + "))";

			return SoapUiJavaScriptHost.Evaluate(expression).GetBoolean();
		}

		// The ranges of a findMatches result as "start-end".
		private static string[] Ranges(JsonElement result)
		{
			AssertOk(result);
			return result.GetProperty("ranges").EnumerateArray().Select(range => range.GetProperty("start").GetInt32() + "-" + range.GetProperty("end").GetInt32()).ToArray();
		}

		// Runs a JavaScript expression with the small DOM installed (see the class comment).
		private static JsonElement WithDom(string expression)
		{
			return SoapUiJavaScriptHost.Evaluate("(function () { " + MiniDom + " return " + expression + "; }())");
		}

		// ------------------------------------------------------------------
		// Helpers: WSDL text and the form template
		// ------------------------------------------------------------------
		// A WSDL with one schema (urn:t, qualified elements) and, for each operation name, a message and an operation. The
		// operation name is also the name of the global element of its input message.
		private static string Wsdl(string schemaBody, params string[] operations)
		{
			return WsdlTypes("<xs:schema elementFormDefault='qualified' targetNamespace='urn:t'>" + schemaBody + "</xs:schema>", operations);
		}

		private static string WsdlTypes(string typesContent, params string[] operations)
		{
			var rest = new StringBuilder();
			var portType = new StringBuilder("<wsdl:portType name='P'>");
			foreach (var operation in operations)
			{
				rest.Append("<wsdl:message name='").Append(operation).Append("In'><wsdl:part name='parameters' element='tns:").Append(operation).Append("'/></wsdl:message>");
				portType.Append("<wsdl:operation name='").Append(operation).Append("'><wsdl:input message='tns:").Append(operation).Append("In'/></wsdl:operation>");
			}

			return WsdlCustom(typesContent, rest.Append(portType).Append("</wsdl:portType>").ToString());
		}

		private static string WsdlCustom(string typesContent, string rest)
		{
			return "<wsdl:definitions xmlns:wsdl='http://schemas.xmlsoap.org/wsdl/' xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:tns='urn:t' xmlns:o='urn:o'"
				+ " xmlns:ser='http://schemas.microsoft.com/2003/10/Serialization/' xmlns:arr='http://schemas.microsoft.com/2003/10/Serialization/Arrays' targetNamespace='urn:t'>"
				+ "<wsdl:types>" + typesContent + "</wsdl:types>" + rest + "</wsdl:definitions>";
		}

		// The global element Op with a sequence and, after it, more content of the complex type (attributes).
		private static string OpElement(string sequenceContent, string complexTypeExtra = "")
		{
			return "<xs:element name='Op'><xs:complexType><xs:sequence>" + sequenceContent + "</xs:sequence>" + complexTypeExtra + "</xs:complexType></xs:element>";
		}

		private static JsonElement Template(string wsdl, string operation)
		{
			return WithDom("window.SoapUiWsdl.buildFormTemplate(window.SoapUiWsdl.parse(" + Lit(wsdl) + ").model, { name: " + Lit(operation) + " }, '1.1')");
		}

		// The template of the operation Op of a WSDL with this schema (it must declare the element Op) and this extra content.
		private static JsonElement TemplateOf(string schemaBody, string extraSchemaContent = "")
		{
			return Template(Wsdl(schemaBody + extraSchemaContent, "Op"), "Op");
		}

		private static string[] Warnings(JsonElement result)
		{
			return Strings(result.GetProperty("template").GetProperty("warnings"));
		}

		// One line for each node and attribute: indentation, name (with {namespace} unless it is urn:t), kind, type, [min..max] and the
		// flags and the note. Attributes come before the children, they start with @.
		private static string Render(JsonElement result)
		{
			AssertOk(result);
			var lines = new List<string>();
			foreach (var part in result.GetProperty("template").GetProperty("bodyParts").EnumerateArray())
			{
				RenderNode(part, string.Empty, lines);
			}

			return string.Join("\n", lines);
		}

		private static void RenderNode(JsonElement node, string indent, List<string> lines)
		{
			var line = new StringBuilder(indent);
			var ns = node.GetProperty("ns").GetString();
			if (ns != DefaultNamespace)
			{
				line.Append('{').Append(ns).Append('}');
			}

			line.Append(node.GetProperty("name").GetString()).Append(' ').Append(node.GetProperty("kind").GetString());
			if (node.GetProperty("type").ValueKind != JsonValueKind.Null)
			{
				line.Append(' ').Append(RenderType(node.GetProperty("type")));
			}

			var max = node.GetProperty("max");
			line.Append(" [").Append(node.GetProperty("min").GetRawText()).Append("..").Append(max.ValueKind == JsonValueKind.Null ? "*" : max.GetRawText()).Append(']');
			if (node.GetProperty("nillable").GetBoolean())
			{
				line.Append(" nillable");
			}

			if (node.GetProperty("choiceGroup").ValueKind == JsonValueKind.String)
			{
				line.Append(" group=").Append(node.GetProperty("choiceGroup").GetString());
			}

			if (node.GetProperty("anyContent").GetBoolean())
			{
				line.Append(" any");
			}

			if (node.GetProperty("truncated").GetBoolean())
			{
				line.Append(" truncated");
			}

			if (node.GetProperty("note").GetString() != string.Empty)
			{
				line.Append(" note=\"").Append(node.GetProperty("note").GetString()).Append('"');
			}

			lines.Add(line.ToString());
			foreach (var attribute in node.GetProperty("attributes").EnumerateArray())
			{
				var attributeLine = new StringBuilder(indent).Append("  @");
				if (attribute.GetProperty("ns").GetString() != string.Empty)
				{
					attributeLine.Append('{').Append(attribute.GetProperty("ns").GetString()).Append('}');
				}

				attributeLine.Append(attribute.GetProperty("name").GetString()).Append(' ').Append(RenderType(attribute.GetProperty("type")));
				if (attribute.GetProperty("required").GetBoolean())
				{
					attributeLine.Append(" required");
				}

				if (attribute.GetProperty("fixed").ValueKind == JsonValueKind.String)
				{
					attributeLine.Append(" fixed=").Append(attribute.GetProperty("fixed").GetString());
				}

				lines.Add(attributeLine.ToString());
			}

			foreach (var child in node.GetProperty("children").EnumerateArray())
			{
				RenderNode(child, indent + "  ", lines);
			}
		}

		private static string RenderType(JsonElement type)
		{
			var text = new StringBuilder(type.GetProperty("xsd").GetString());
			if (type.GetProperty("enumValues").ValueKind == JsonValueKind.Array)
			{
				text.Append('{').Append(string.Join("|", Strings(type.GetProperty("enumValues")))).Append('}');
			}

			if (type.GetProperty("pattern").ValueKind == JsonValueKind.String)
			{
				text.Append(" /").Append(type.GetProperty("pattern").GetString()).Append('/');
			}

			return text.ToString();
		}

		private static string ManyValuesType(int count)
		{
			var values = new StringBuilder();
			for (var index = 0; index < count; index++)
			{
				values.Append("<xs:enumeration value='v").Append(index).Append("'/>");
			}

			return "<xs:simpleType name='Many'><xs:restriction base='xs:string'>" + values + "</xs:restriction></xs:simpleType>";
		}

		// The element Op, nested inline elements N2 to N<depth> in it. The last one is a string.
		private static string NestedElements(int depth)
		{
			var inner = "<xs:element name='N" + depth + "' type='xs:string'/>";
			for (var level = depth - 1; level >= 2; level--)
			{
				inner = "<xs:element name='N" + level + "'><xs:complexType><xs:sequence>" + inner + "</xs:sequence></xs:complexType></xs:element>";
			}

			return OpElement(inner);
		}

		// The element Op with width elements of the type L1, which has width elements of the type L2, and so on down to the type of the given number
		// of levels, which has width strings. That makes (width ^ (levels + 2) - 1) / (width - 1) nodes with the root.
		private static string FanOut(int width, int levels)
		{
			var types = new StringBuilder();
			for (var level = levels; level >= 1; level--)
			{
				var members = new StringBuilder();
				for (var index = 0; index < width; index++)
				{
					members.Append(level == levels ? "<xs:element name='S" + index + "' type='xs:string'/>" : "<xs:element name='M" + index + "' type='tns:L" + (level + 1) + "'/>");
				}

				types.Append("<xs:complexType name='L").Append(level).Append("'><xs:sequence>").Append(members).Append("</xs:sequence></xs:complexType>");
			}

			var rootMembers = new StringBuilder();
			for (var index = 0; index < width; index++)
			{
				rootMembers.Append("<xs:element name='R").Append(index).Append("' type='tns:L1'/>");
			}

			return OpElement(rootMembers.ToString()) + types;
		}

		private static IEnumerable<JsonElement> Descendants(JsonElement node)
		{
			yield return node;
			foreach (var child in node.GetProperty("children").EnumerateArray())
			{
				foreach (var descendant in Descendants(child))
				{
					yield return descendant;
				}
			}
		}

		private static string NestedSequences(int count, string inner)
		{
			var content = inner;
			for (var index = 0; index < count; index++)
			{
				content = "<xs:sequence>" + content + "</xs:sequence>";
			}

			return content;
		}

		// A WSDL with the operation Op that works and four operations that the envelope builder and the form template refuse.
		private static string RefusedOperationsWsdl()
		{
			var types = "<xs:schema elementFormDefault='qualified' targetNamespace='urn:t'>" + OpElement("<xs:element name='A' type='xs:string'/>") + "</xs:schema>";
			var rest = "<wsdl:message name='OpIn'><wsdl:part name='parameters' element='tns:Op'/></wsdl:message>"
				+ "<wsdl:message name='rpcIn'><wsdl:part name='a' type='xs:string'/></wsdl:message>"
				+ "<wsdl:message name='emptyIn'/>"
				+ "<wsdl:message name='undeclaredIn'><wsdl:part name='parameters' element='tns:NoSuchElement'/></wsdl:message>"
				+ "<wsdl:portType name='P'>"
				+ "<wsdl:operation name='Op'><wsdl:input message='tns:OpIn'/></wsdl:operation>"
				+ "<wsdl:operation name='rpc'><wsdl:input message='tns:rpcIn'/></wsdl:operation>"
				+ "<wsdl:operation name='noInput'><wsdl:output message='tns:OpIn'/></wsdl:operation>"
				+ "<wsdl:operation name='empty'><wsdl:input message='tns:emptyIn'/></wsdl:operation>"
				+ "<wsdl:operation name='undeclared'><wsdl:input message='tns:undeclaredIn'/></wsdl:operation>"
				+ "</wsdl:portType>";
			return WsdlCustom(types, rest);
		}

		private static string DescribeBinding(JsonElement binding)
		{
			var operations = binding.GetProperty("operations").EnumerateArray().Select(operation => operation.GetProperty("name").GetString() + "=" + operation.GetProperty("soapAction").GetString());
			return string.Join(
				"|",
				binding.GetProperty("name").GetString(),
				binding.GetProperty("type").GetString(),
				binding.GetProperty("transport").GetString(),
				binding.GetProperty("style").GetString(),
				binding.GetProperty("soapVersion").GetString(),
				string.Join(";", operations));
		}

		private static string DescribePortType(JsonElement portType)
		{
			var operations = portType.GetProperty("operations").EnumerateArray().Select(operation => operation.GetProperty("name").GetString()
				+ "(" + operation.GetProperty("input").GetString() + ")->" + operation.GetProperty("output").GetString()
				+ "!" + string.Join(",", Strings(operation.GetProperty("faults"))));
			return portType.GetProperty("name").GetString() + ":" + string.Join(";", operations);
		}

		private static string DescribeMessage(JsonElement message)
		{
			var parts = message.GetProperty("parts").EnumerateArray().Select(part => part.GetProperty("name").GetString() + "=" + part.GetProperty("element").GetString() + "/" + part.GetProperty("type").GetString());
			return message.GetProperty("name").GetString() + ":" + string.Join(";", parts);
		}
	}
}
