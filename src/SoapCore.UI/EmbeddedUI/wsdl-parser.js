/*
 * wsdl-parser.js
 *
 * Page independent logic of the SoapCore.UI explorer: WSDL parsing, sample envelope
 * generation, XML pretty printing and checking, SOAP fault parsing, request validation,
 * the code snippet builders (curl, PowerShell, C# and raw HTTP), the authorization headers
 * (Bearer, Basic, API key and the WS-Security UsernameToken), small display, link and
 * command helpers, the data behind the form editor (form template, input kinds, value
 * validation), the XML tokenizer used for syntax highlighting, the text search and the
 * structured outline of a WSDL.
 * Everything is exposed on the frozen global SoapUiWsdl.
 *
 * Rules for this file:
 *  - Markup is never assembled from strings. Samples are built with DOM calls and then
 *    serialised, so every value is escaped by the serialiser. The one documented exception is
 *    buildWsSecurityHeader, which joins fixed element names and escaped values (escapeXml for
 *    text, escapeAttribute for attribute values) and whose result applyWsSecurity parses again.
 *  - Parsed documents are only read, except by applyWsSecurity, which edits its own transient
 *    copy of the envelope. Nodes are never moved into the page.
 *  - The functions that need a DOM are parse, buildEnvelope, fallbackEnvelope, prettyPrintXml,
 *    checkXml, parseSoapFault, faultFromText, validateEnvelope, applyWsSecurity, buildFormTemplate
 *    and buildWsdlOutline. The others, the form helpers and the text tools included, are plain
 *    JavaScript.
 *  - Nothing is logged, stored or sent anywhere.
 *  - The public functions never throw. They return { ok: false, error } instead
 *    (prettyPrintXml is the documented exception, its caller falls back to raw text).
 */
(function () {
	'use strict';

	const NS_WSDL = 'http://schemas.xmlsoap.org/wsdl/';
	const NS_XSD = 'http://www.w3.org/2001/XMLSchema';
	const NS_SOAP11 = 'http://schemas.xmlsoap.org/soap/envelope/';
	const NS_SOAP12 = 'http://www.w3.org/2003/05/soap-envelope';
	const NS_XMLNS = 'http://www.w3.org/2000/xmlns/';
	const NS_XML = 'http://www.w3.org/XML/1998/namespace';
	const NS_MS_SERIALIZATION = 'http://schemas.microsoft.com/2003/10/Serialization/';
	const NS_MS_ARRAYS = 'http://schemas.microsoft.com/2003/10/Serialization/Arrays';

	// WS-Security (OASIS) identifiers: XML namespaces and token profile types, never fetched. They are joined from two
	// parts, so this file holds no absolute URL beyond the XML namespaces the embedded content tests allow.
	const OASIS_WSS = 'http://docs.oasis-open.org/wss/2004/01/';
	const NS_WSSE = OASIS_WSS + 'oasis-200401-wss-wssecurity-secext-1.0.xsd';
	const NS_WSU = OASIS_WSS + 'oasis-200401-wss-wssecurity-utility-1.0.xsd';
	const WSS_PASSWORD_TEXT = OASIS_WSS + 'oasis-200401-wss-username-token-profile-1.0#PasswordText';
	const WSS_PASSWORD_DIGEST = OASIS_WSS + 'oasis-200401-wss-username-token-profile-1.0#PasswordDigest';
	const WSS_BASE64_BINARY = OASIS_WSS + 'oasis-200401-wss-soap-message-security-1.0#Base64Binary';

	const GUID_SAMPLE = '00000000-0000-0000-0000-000000000000';
	const DATE_TIME_OFFSET_SAMPLE = '2025-01-01T12:00:00+00:00';
	const SAMPLE_DATE_TIME = '2025-01-01T12:00:00Z';
	const SAMPLE_DATE = '2025-01-01';
	const UNSUPPORTED = 'unsupported schema construct: ';
	const BYTE_UNITS = Object.freeze(['B', 'KB', 'MB', 'GB', 'TB']);
	const SVCUTIL_URL = /^https?:\/\/[A-Za-z0-9._~:\/?#\[\]@!*+,=%-]+$/;

	const LIMITS = Object.freeze({
		maxNodes: 2000,
		maxBytes: 256 * 1024,
		budgetBytes: 248 * 1024,
		maxDepth: 10,
		maxParticleNesting: 50,
		maxSampleText: 500,
		maxEnumListed: 20,
		maxIndent: 64
	});

	const XML_ESCAPES = new Map([['&', '&amp;'], ['<', '&lt;'], ['>', '&gt;'], ['"', '&quot;'], ["'", '&#39;']]);

	const FORBIDDEN_HEADERS = new Set([
		'host', 'content-length', 'connection', 'cookie', 'cookie2', 'origin', 'referer', 'date', 'expect',
		'keep-alive', 'te', 'trailer', 'transfer-encoding', 'upgrade', 'via', 'dnt', 'accept-charset', 'accept-encoding'
	]);
	const FORBIDDEN_HEADER_PREFIXES = ['proxy-', 'sec-', 'access-control-request-'];
	const OWNED_HEADERS = new Set(['content-type', 'soapaction']);

	// ------------------------------------------------------------------
	// Small helpers
	// ------------------------------------------------------------------

	function fail(error) {
		return { ok: false, error: error };
	}

	function describeError(error) {
		if (error && typeof error.message === 'string' && error.message !== '') {
			return error.message;
		}

		return 'unexpected error';
	}

	function shorten(text, max) {
		const clean = String(text === null || text === undefined ? '' : text).replace(/\s+/g, ' ').trim();
		return clean.length > max ? clean.slice(0, max - 3) + '...' : clean;
	}

	function hasDoctype(text) {
		return /<!DOCTYPE|<!ENTITY/i.test(String(text));
	}

	function escapeXml(value) {
		return String(value).replace(/[&<>"']/g, (character) => XML_ESCAPES.get(character));
	}

	function escapeText(value) {
		return String(value).replace(/[&<>]/g, (character) => XML_ESCAPES.get(character));
	}

	function escapeAttribute(value) {
		return String(value)
			.replace(/[&<>"]/g, (character) => XML_ESCAPES.get(character))
			.replace(/\t/g, '&#9;')
			.replace(/\n/g, '&#10;')
			.replace(/\r/g, '&#13;');
	}

	function envelopeNamespace(version) {
		if (version === '1.1') {
			return NS_SOAP11;
		}

		if (version === '1.2') {
			return NS_SOAP12;
		}

		return null;
	}

	function isMsSerialization(ns) {
		return typeof ns === 'string' && ns.startsWith(NS_MS_SERIALIZATION);
	}

	function qualifiedKey(ns, local) {
		return '{' + ns + '}' + local;
	}

	// Reads { message, line } from the text a browser puts in a parsererror element. Chrome and Safari word it
	// "error on line N at column M: ...", Firefox "XML Parsing Error: ... Line Number N, Column M". The line is 0 when unknown.
	function describeXmlError(raw) {
		const text = String(raw);
		const libxml = /error on line (\d+) at column \d+:\s*([^\n]*?)\s*(?:Below is a rendering|\n|$)/i.exec(text);
		if (libxml) {
			return { message: shorten(libxml[2], 160), line: Number(libxml[1]) };
		}

		const gecko = /XML Parsing Error:\s*([^\n]*)[\s\S]*?Line Number (\d+)/i.exec(text);
		if (gecko) {
			return { message: shorten(gecko[1], 160), line: Number(gecko[2]) };
		}

		return { message: shorten(text, 160), line: 0 };
	}

	function notWellFormed(message, line) {
		const detail = line > 0 ? message + ' (line ' + line + ')' : message;
		return { error: 'The document is not well-formed XML. ' + detail, detail: detail, message: message, line: line };
	}

	// Returns { doc } or { error, ... }. A document that is not well-formed also carries detail, message and line.
	function parseXmlDocument(text) {
		if (typeof text !== 'string' || text.trim() === '') {
			return { error: 'The document is empty.' };
		}

		if (hasDoctype(text)) {
			return { error: 'DOCTYPE and ENTITY declarations are not supported.' };
		}

		// Plain text and similar bodies are rejected here. Browsers build an error document with
		// inline styles for text that fails to parse, which a strict CSP reports as an error in the browser developer tools.
		if (!/^\uFEFF?\s*</.test(text)) {
			return notWellFormed('The text does not start with an XML element.', 0);
		}

		let doc;
		try {
			doc = new DOMParser().parseFromString(text, 'application/xml');
		} catch (error) {
			return { error: 'The document could not be parsed: ' + describeError(error) };
		}

		const problems = doc.getElementsByTagNameNS('*', 'parsererror');
		if (problems.length > 0) {
			const info = describeXmlError(problems[0].textContent);
			return notWellFormed(info.message, info.line);
		}

		return { doc: doc };
	}

	// Live check of editor text. Returns { ok: true } or { ok: false, error, line }, where error is the bare message
	// and line is 0 when the browser did not report one. Never throws.
	function checkXml(text) {
		try {
			const parsed = parseXmlDocument(text);
			if (parsed.doc) {
				return { ok: true };
			}

			return { ok: false, error: parsed.message || parsed.error, line: parsed.line || 0 };
		} catch (error) {
			return { ok: false, error: describeError(error), line: 0 };
		}
	}

	function childElements(node) {
		const result = [];
		for (let child = node.firstElementChild; child; child = child.nextElementSibling) {
			result.push(child);
		}

		return result;
	}

	function xsdChildren(node) {
		return childElements(node).filter((child) => child.namespaceURI === NS_XSD);
	}

	function firstXsdChild(node, local) {
		for (let child = node.firstElementChild; child; child = child.nextElementSibling) {
			if (child.namespaceURI === NS_XSD && child.localName === local) {
				return child;
			}
		}

		return null;
	}

	// Resolves a QName attribute value using the namespace declarations in scope of the node.
	function resolveQName(node, value) {
		if (typeof value !== 'string' || value.trim() === '') {
			return null;
		}

		const text = value.trim();
		const colon = text.indexOf(':');
		const prefix = colon >= 0 ? text.slice(0, colon) : null;
		const local = colon >= 0 ? text.slice(colon + 1) : text;
		if (local === '') {
			return null;
		}

		const ns = node.lookupNamespaceURI(prefix) || '';
		return { ns: ns, local: local, key: qualifiedKey(ns, local) };
	}

	// ------------------------------------------------------------------
	// XML pretty printer (DOM based and iterative, so nesting depth cannot overflow the stack)
	// ------------------------------------------------------------------

	function attributesText(element) {
		let result = '';
		for (let index = 0; index < element.attributes.length; index += 1) {
			const attribute = element.attributes[index];
			result += ' ' + attribute.name + '="' + escapeAttribute(attribute.value) + '"';
		}

		return result;
	}

	// Returns 'empty', 'text' (one line) or 'block' (children on their own lines).
	function classifyContent(element) {
		if (!element.firstChild) {
			return 'empty';
		}

		for (let child = element.firstChild; child; child = child.nextSibling) {
			if (child.nodeType !== 3 && child.nodeType !== 4) {
				return 'block';
			}
		}

		let blank = true;
		for (let child = element.firstChild; child; child = child.nextSibling) {
			if (child.nodeType === 4 || child.data.trim() !== '') {
				blank = false;
			}
		}

		// Indentation only (spaces around a line break) counts as empty. A lone space is data.
		if (blank && /[\r\n]/.test(element.textContent)) {
			return 'empty';
		}

		return 'text';
	}

	function inlineText(element) {
		let result = '';
		for (let child = element.firstChild; child; child = child.nextSibling) {
			result += child.nodeType === 4 ? '<![CDATA[' + child.data + ']]>' : escapeText(child.data);
		}

		return result;
	}

	function pushChildNodes(stack, parent, depth) {
		for (let child = parent.lastChild; child; child = child.previousSibling) {
			stack.push({ node: child, depth: depth, closeName: undefined });
		}
	}

	function printXmlNodes(root, out) {
		const stack = [];
		pushChildNodes(stack, root, 0);
		while (stack.length > 0) {
			const item = stack.pop();
			const pad = '\t'.repeat(Math.min(item.depth, LIMITS.maxIndent));
			if (item.closeName !== undefined) {
				out.push(pad + '</' + item.closeName + '>');
				continue;
			}

			const node = item.node;
			switch (node.nodeType) {
				case 1: {
					const open = '<' + node.tagName + attributesText(node);
					const kind = classifyContent(node);
					if (kind === 'empty') {
						out.push(pad + open + '/>');
					} else if (kind === 'text') {
						out.push(pad + open + '>' + inlineText(node) + '</' + node.tagName + '>');
					} else {
						out.push(pad + open + '>');
						stack.push({ node: null, depth: item.depth, closeName: node.tagName });
						pushChildNodes(stack, node, item.depth + 1);
					}

					break;
				}

				case 3: {
					const text = node.data.trim();
					if (text !== '') {
						out.push(pad + escapeText(text));
					}

					break;
				}

				case 4:
					out.push(pad + '<![CDATA[' + node.data + ']]>');
					break;
				case 7:
					out.push(pad + '<?' + node.target + (node.data ? ' ' + node.data : '') + '?>');
					break;
				case 8:
					out.push(pad + '<!--' + node.data + '-->');
					break;
				default:
					break;
			}
		}
	}

	// Pretty prints well-formed XML with tab indentation. Throws when the text is not
	// well-formed XML or declares a DOCTYPE. Callers fall back to the raw text.
	function prettyPrintXml(text) {
		const parsed = parseXmlDocument(text);
		if (!parsed.doc) {
			throw new Error(parsed.error);
		}

		const out = [];
		const declaration = /^\uFEFF?\s*(<\?xml\s[^>]*?\?>)/.exec(text);
		if (declaration) {
			out.push(declaration[1]);
		}

		printXmlNodes(parsed.doc, out);
		return out.join('\n');
	}

	// ------------------------------------------------------------------
	// SOAP faults
	// ------------------------------------------------------------------

	function directChild(parent, namespaces, local) {
		for (let child = parent.firstElementChild; child; child = child.nextElementSibling) {
			if (child.localName === local && (namespaces === null || namespaces.indexOf(child.namespaceURI) !== -1)) {
				return child;
			}
		}

		return null;
	}

	function textOf(node) {
		return node ? node.textContent.trim() : '';
	}

	// The pretty printed detail element of a fault (detail in SOAP 1.1, Detail in SOAP 1.2), or '' when there is none.
	function faultDetailText(fault, envelopeNamespaces) {
		const detail = directChild(fault, null, 'detail') || directChild(fault, envelopeNamespaces, 'Detail');
		if (!detail || (!detail.firstElementChild && textOf(detail) === '')) {
			return '';
		}

		try {
			return prettyPrintXml(new XMLSerializer().serializeToString(detail));
		} catch (error) {
			return textOf(detail);
		}
	}

	// Returns { code, reason, detail } for a SOAP 1.1 or 1.2 fault document, otherwise null.
	function parseSoapFault(doc) {
		if (!doc || !doc.documentElement) {
			return null;
		}

		const envelopeNamespaces = [NS_SOAP11, NS_SOAP12];
		const root = doc.documentElement;
		if (root.localName !== 'Envelope' || envelopeNamespaces.indexOf(root.namespaceURI) === -1) {
			return null;
		}

		const body = directChild(root, envelopeNamespaces, 'Body');
		const fault = body ? directChild(body, envelopeNamespaces, 'Fault') : null;
		if (!fault) {
			return null;
		}

		// SOAP 1.1 uses faultcode and faultstring, SOAP 1.2 uses Code/Value and Reason/Text.
		let code = textOf(directChild(fault, null, 'faultcode'));
		let reason = textOf(directChild(fault, null, 'faultstring'));
		if (code === '') {
			const codeElement = directChild(fault, envelopeNamespaces, 'Code');
			code = textOf(codeElement ? directChild(codeElement, envelopeNamespaces, 'Value') : null);
		}

		if (reason === '') {
			const reasonElement = directChild(fault, envelopeNamespaces, 'Reason');
			reason = textOf(reasonElement ? directChild(reasonElement, envelopeNamespaces, 'Text') : null);
		}

		return { code: code, reason: reason, detail: faultDetailText(fault, envelopeNamespaces) };
	}

	function faultFromText(text) {
		const parsed = parseXmlDocument(text);
		return parsed.doc ? parseSoapFault(parsed.doc) : null;
	}

	// ------------------------------------------------------------------
	// WSDL model
	// ------------------------------------------------------------------

	function collectSchema(model, node) {
		const info = {
			ns: node.getAttribute('targetNamespace') || '',
			qualifiedElements: node.getAttribute('elementFormDefault') === 'qualified',
			qualifiedAttributes: node.getAttribute('attributeFormDefault') === 'qualified'
		};
		model.schemaCount += 1;
		for (const child of xsdChildren(node)) {
			const name = child.getAttribute('name');
			if (!name) {
				continue;
			}

			let target = null;
			switch (child.localName) {
				case 'element':
					target = model.elements;
					break;
				case 'complexType':
					target = model.complexTypes;
					break;
				case 'simpleType':
					target = model.simpleTypes;
					break;
				case 'attribute':
					target = model.attributes;
					break;
				default:
					break;
			}

			const key = qualifiedKey(info.ns, name);
			if (target && !target.has(key)) {
				target.set(key, { node: child, schema: info });
			}
		}
	}

	function collectMessage(model, node) {
		const name = node.getAttribute('name');
		if (!name || model.messages.has(name)) {
			return;
		}

		const parts = [];
		for (const child of childElements(node)) {
			if (child.namespaceURI === NS_WSDL && child.localName === 'part') {
				parts.push({
					name: child.getAttribute('name') || '',
					element: resolveQName(child, child.getAttribute('element')),
					type: resolveQName(child, child.getAttribute('type'))
				});
			}
		}

		model.messages.set(name, { name: name, parts: parts });
	}

	function collectPortType(model, node) {
		for (const operation of childElements(node)) {
			if (operation.namespaceURI !== NS_WSDL || operation.localName !== 'operation') {
				continue;
			}

			// The same operation name can appear in several port types. The first one wins.
			const name = operation.getAttribute('name');
			if (!name || model.operations.has(name)) {
				continue;
			}

			let inputMessage = '';
			for (const child of childElements(operation)) {
				if (child.namespaceURI === NS_WSDL && child.localName === 'input') {
					const reference = resolveQName(child, child.getAttribute('message'));
					inputMessage = reference ? reference.local : '';
					break;
				}
			}

			model.operations.set(name, { name: name, inputMessage: inputMessage });
		}
	}

	function parseModel(text) {
		const parsed = parseXmlDocument(text);
		if (!parsed.doc) {
			return fail(parsed.error);
		}

		const root = parsed.doc.documentElement;
		if (!root || root.namespaceURI !== NS_WSDL || root.localName !== 'definitions') {
			return fail('The document is not a WSDL 1.1 definitions element.');
		}

		// All maps are keyed by names taken from the document, so they are Maps and never plain objects.
		const model = {
			targetNamespace: root.getAttribute('targetNamespace') || '',
			schemaCount: 0,
			elements: new Map(),
			complexTypes: new Map(),
			simpleTypes: new Map(),
			attributes: new Map(),
			messages: new Map(),
			operations: new Map()
		};

		for (const section of childElements(root)) {
			if (section.namespaceURI !== NS_WSDL) {
				continue;
			}

			switch (section.localName) {
				case 'types':
					for (const schema of xsdChildren(section)) {
						if (schema.localName === 'schema') {
							collectSchema(model, schema);
						}
					}

					break;
				case 'message':
					collectMessage(model, section);
					break;
				case 'portType':
					collectPortType(model, section);
					break;
				default:
					break;
			}
		}

		return { ok: true, model: model };
	}

	// Parses WSDL text. Returns { ok: true, model } or { ok: false, error }. Never throws.
	function parse(text) {
		try {
			return parseModel(text);
		} catch (error) {
			return fail('The WSDL could not be processed: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// Sample values
	// ------------------------------------------------------------------

	function builtinSample(local) {
		switch (local) {
			case 'boolean':
				return 'true';
			case 'int':
			case 'integer':
			case 'long':
			case 'short':
			case 'byte':
			case 'unsignedInt':
			case 'unsignedLong':
			case 'unsignedShort':
			case 'unsignedByte':
			case 'positiveInteger':
				return '1';
			case 'nonNegativeInteger':
			case 'nonPositiveInteger':
				return '0';
			case 'negativeInteger':
				return '-1';
			case 'decimal':
			case 'float':
			case 'double':
				return '1.5';
			case 'dateTime':
				return SAMPLE_DATE_TIME;
			case 'date':
				return SAMPLE_DATE;
			case 'time':
				return '12:00:00';
			case 'duration':
				return 'PT1H';
			case 'base64Binary':
				return 'AAAA';
			case 'hexBinary':
				return '00';
			case 'anyURI':
				return 'https://example.com';
			case 'guid':
				return GUID_SAMPLE;
			case 'char':
				return '65';
			default:
				return 'string';
		}
	}

	function enumerationValues(restriction) {
		const values = [];
		for (const child of xsdChildren(restriction)) {
			if (child.localName === 'enumeration' && child.getAttribute('value') !== null) {
				values.push(child.getAttribute('value'));
			}
		}

		return values;
	}

	// Returns { text, note } for a simple type element (xs:simpleType).
	function sampleForSimpleTypeNode(ctx, node, guard) {
		for (const child of xsdChildren(node)) {
			if (child.localName === 'restriction') {
				const values = enumerationValues(child);
				let base = null;
				const baseName = resolveQName(child, child.getAttribute('base'));
				const inlineType = firstXsdChild(child, 'simpleType');
				if (baseName) {
					base = simpleSampleForType(ctx, baseName, guard);
				} else if (inlineType) {
					base = sampleForSimpleTypeNode(ctx, inlineType, guard);
				}

				if (values.length > 0) {
					const listed = values.slice(0, LIMITS.maxEnumListed).join(' | ');
					return { text: values[0], note: 'one of: ' + listed + (values.length > LIMITS.maxEnumListed ? ' | ...' : '') };
				}

				return base || { text: 'string', note: '' };
			}

			if (child.localName === 'list' || child.localName === 'union') {
				return { text: 'string', note: UNSUPPORTED + 'simpleType/' + child.localName };
			}
		}

		return { text: 'string', note: '' };
	}

	// Returns { text, note } when the QName is a simple type, otherwise null.
	function simpleSampleForType(ctx, type, guard) {
		if (type.ns === NS_XSD) {
			return { text: builtinSample(type.local), note: '' };
		}

		// A guid is sampled by its name whatever namespace declares it.
		if (type.local === 'guid') {
			return { text: GUID_SAMPLE, note: '' };
		}

		if (type.local === 'char' && isMsSerialization(type.ns)) {
			return { text: '65', note: '' };
		}

		const declared = ctx.model ? ctx.model.simpleTypes.get(type.key) : undefined;
		if (declared) {
			if (guard.has(type.key) || guard.size > LIMITS.maxDepth) {
				return { text: 'string', note: 'recursive simple type: ' + type.local };
			}

			guard.add(type.key);
			const result = sampleForSimpleTypeNode(ctx, declared.node, guard);
			guard.delete(type.key);
			return result;
		}

		if (isMsSerialization(type.ns)) {
			return { text: builtinSample(type.local), note: '' };
		}

		return null;
	}

	// ------------------------------------------------------------------
	// Sample envelope builder
	// ------------------------------------------------------------------

	function createContext(model, envelopeNs) {
		const doc = document.implementation.createDocument(envelopeNs, 'soapenv:Envelope', null);
		const root = doc.documentElement;
		root.setAttributeNS(NS_XMLNS, 'xmlns:soapenv', envelopeNs);
		root.appendChild(doc.createElementNS(envelopeNs, 'soapenv:Header'));
		const body = doc.createElementNS(envelopeNs, 'soapenv:Body');
		root.appendChild(body);
		return {
			model: model,
			doc: doc,
			root: root,
			body: body,
			prefixes: new Map(),
			seen: new Set(),
			depthParents: new WeakSet(),
			nodes: 0,
			bytes: 0,
			nesting: 0,
			exhausted: false,
			truncationNoted: false,
			depthNoted: false,
			warnings: []
		};
	}

	// Allocates tns, q1, q2, ... for a namespace and declares it on the envelope root.
	// A default namespace is never declared, so unqualified elements stay unqualified.
	function prefixFor(ctx, ns) {
		if (!ns) {
			return '';
		}

		let prefix = ctx.prefixes.get(ns);
		if (prefix === undefined) {
			prefix = ctx.prefixes.size === 0 ? 'tns' : 'q' + ctx.prefixes.size;
			ctx.prefixes.set(ns, prefix);
			ctx.root.setAttributeNS(NS_XMLNS, 'xmlns:' + prefix, ns);
			ctx.bytes += prefix.length + ns.length + 10;
		}

		return prefix;
	}

	function reserve(ctx, cost) {
		if (ctx.exhausted) {
			return false;
		}

		if (ctx.nodes >= LIMITS.maxNodes || ctx.bytes + cost > LIMITS.budgetBytes) {
			ctx.exhausted = true;
			return false;
		}

		ctx.nodes += 1;
		ctx.bytes += cost;
		return true;
	}

	function noteTruncated(ctx, parent) {
		if (ctx.truncationNoted) {
			return;
		}

		ctx.truncationNoted = true;
		parent.appendChild(ctx.doc.createComment(' sample truncated: size limit reached '));
		ctx.warnings.push('The sample was truncated because it reached the size limit (2,000 elements or 256 KB).');
	}

	// A comment must not contain a double hyphen and must not start or end with a hyphen.
	// Angle brackets are dropped as well, so schema supplied text can never look like markup.
	function sanitizeComment(text) {
		let clean = String(text).replace(/[\u0000-\u001F\u007F-\u009F\uFFFE\uFFFF<>]/g, ' ').trim();
		while (clean.indexOf('--') !== -1) {
			clean = clean.replace(/--/g, '- -');
		}

		if (clean.length > 200) {
			clean = clean.slice(0, 197) + '...';
		}

		return clean;
	}

	function createCommentNode(ctx, text) {
		// Besides the delimiters, a comment gets its own indented line when the sample is pretty printed.
		const body = ' ' + sanitizeComment(text) + ' ';
		const cost = body.length + 7 + LIMITS.maxDepth + 3;
		if (ctx.bytes + cost > LIMITS.budgetBytes) {
			ctx.exhausted = true;
			return null;
		}

		ctx.bytes += cost;
		return ctx.doc.createComment(body);
	}

	function addComment(ctx, parent, text) {
		const comment = createCommentNode(ctx, text);
		if (comment) {
			parent.appendChild(comment);
		}
	}

	function addCommentBefore(ctx, node, text) {
		const comment = createCommentNode(ctx, text);
		if (comment && node.parentNode) {
			node.parentNode.insertBefore(comment, node);
		}
	}

	function appendText(ctx, element, text) {
		const value = String(text).slice(0, LIMITS.maxSampleText);
		ctx.bytes += value.length;
		if (ctx.bytes > LIMITS.budgetBytes) {
			ctx.exhausted = true;
		}

		element.appendChild(ctx.doc.createTextNode(value));
	}

	function createElement(ctx, parent, ns, local, depth) {
		const prefix = prefixFor(ctx, ns);
		const qualifiedName = prefix ? prefix + ':' + local : local;
		if (!reserve(ctx, qualifiedName.length * 2 + 6 + depth * 2)) {
			noteTruncated(ctx, parent);
			return null;
		}

		let element;
		try {
			element = ctx.doc.createElementNS(ns || null, qualifiedName);
		} catch (error) {
			addComment(ctx, parent, 'element skipped, the name is not valid XML: ' + local);
			return null;
		}

		parent.appendChild(element);
		return element;
	}

	function describeOccurs(node) {
		const min = node.getAttribute('minOccurs');
		const max = node.getAttribute('maxOccurs');
		const optional = min === '0';
		const repeatable = max === 'unbounded' || (max !== null && Number(max) > 1);
		if (optional && repeatable) {
			return 'optional, repeatable';
		}

		if (optional) {
			return 'optional';
		}

		return repeatable ? 'repeatable' : '';
	}

	// The sample keeps a value for a nillable element, the note only tells the user that xsi:nil is allowed.
	function describeNillable(declaration) {
		const value = declaration.getAttribute('nillable');
		return value === 'true' || value === '1' ? 'nillable' : '';
	}

	function describeAlternative(node) {
		if (node.localName === 'element') {
			return node.getAttribute('name') || node.getAttribute('ref') || 'element';
		}

		return node.localName;
	}

	function buildAttribute(ctx, element, node, schema) {
		if (node.getAttribute('use') === 'prohibited') {
			return;
		}

		let declaration = node;
		let name = node.getAttribute('name');
		let ns = '';
		let inXmlNamespace = false;
		const referenceValue = node.getAttribute('ref');
		if (referenceValue !== null) {
			const reference = resolveQName(node, referenceValue);
			if (!reference) {
				return;
			}

			if (reference.ns === NS_XML) {
				name = reference.local;
				inXmlNamespace = true;
			} else if (isMsSerialization(reference.ns)) {
				// Serializer plumbing such as Id and Ref. It is never needed in a sample.
				return;
			} else {
				const target = ctx.model ? ctx.model.attributes.get(reference.key) : undefined;
				if (!target) {
					addComment(ctx, element, 'unresolved attribute reference: ' + reference.local);
					return;
				}

				declaration = target.node;
				name = declaration.getAttribute('name');
				ns = target.schema.ns;
			}
		} else {
			const form = node.getAttribute('form');
			const qualified = form === 'qualified' || (form !== 'unqualified' && schema.qualifiedAttributes);
			ns = qualified ? schema.ns : '';
		}

		// Namespace declarations are managed by the builder. Never let a schema create one.
		if (!name || name === 'xmlns' || name.startsWith('xmlns:')) {
			return;
		}

		let value = declaration.getAttribute('fixed');
		if (value === null) {
			value = declaration.getAttribute('default');
		}

		if (value === null) {
			value = attributeSample(ctx, declaration);
		}

		try {
			if (inXmlNamespace) {
				element.setAttributeNS(NS_XML, 'xml:' + name, value);
			} else if (ns) {
				element.setAttributeNS(ns, prefixFor(ctx, ns) + ':' + name, value);
			} else {
				element.setAttribute(name, value);
			}

			ctx.bytes += name.length + value.length + 4;
		} catch (error) {
			addComment(ctx, element, 'attribute skipped, the name is not valid XML: ' + name);
		}
	}

	function attributeSample(ctx, declaration) {
		const inlineType = firstXsdChild(declaration, 'simpleType');
		if (inlineType) {
			return sampleForSimpleTypeNode(ctx, inlineType, new Set()).text;
		}

		const type = resolveQName(declaration, declaration.getAttribute('type'));
		const sample = type ? simpleSampleForType(ctx, type, new Set()) : null;
		return sample ? sample.text : 'string';
	}

	function fillComplexType(ctx, element, node, schema, depth) {
		for (const child of xsdChildren(node)) {
			if (ctx.exhausted) {
				return;
			}

			switch (child.localName) {
				case 'sequence':
				case 'all':
				case 'choice':
					fillParticle(ctx, element, child, schema, depth);
					break;
				case 'complexContent':
					fillComplexContent(ctx, element, child, schema, depth);
					break;
				case 'attribute':
					buildAttribute(ctx, element, child, schema);
					break;
				case 'simpleContent':
				case 'group':
				case 'attributeGroup':
					addComment(ctx, element, UNSUPPORTED + child.localName);
					break;
				default:
					break;
			}
		}
	}

	function fillComplexContent(ctx, element, node, schema, depth) {
		for (const child of xsdChildren(node)) {
			if (ctx.exhausted) {
				return;
			}

			if (child.localName === 'extension') {
				const base = resolveQName(child, child.getAttribute('base'));
				if (base && base.ns !== NS_XSD) {
					const declared = ctx.model ? ctx.model.complexTypes.get(base.key) : undefined;
					if (declared && !ctx.seen.has(base.key)) {
						// Members of the base type come first, as they do on the wire.
						ctx.seen.add(base.key);
						fillComplexType(ctx, element, declared.node, declared.schema, depth);
						ctx.seen.delete(base.key);
					} else if (declared) {
						addComment(ctx, element, 'recursive base type: ' + base.local);
					} else {
						addComment(ctx, element, 'unresolved base type: ' + base.local);
					}
				}

				fillComplexType(ctx, element, child, schema, depth);
			} else if (child.localName === 'restriction') {
				addComment(ctx, element, UNSUPPORTED + 'complexContent/restriction');
			}
		}
	}

	function fillParticleChild(ctx, element, child, schema, depth) {
		switch (child.localName) {
			case 'element':
				buildElement(ctx, element, child, schema, false, depth + 1);
				break;
			case 'sequence':
			case 'all':
			case 'choice':
				fillParticle(ctx, element, child, schema, depth);
				break;
			case 'any':
				addComment(ctx, element, 'any element is allowed here');
				break;
			case 'group':
				addComment(ctx, element, UNSUPPORTED + 'group');
				break;
			default:
				break;
		}
	}

	function fillParticle(ctx, element, node, schema, depth) {
		if (ctx.nesting >= LIMITS.maxParticleNesting) {
			addComment(ctx, element, 'schema nesting limit reached');
			return;
		}

		ctx.nesting += 1;
		const children = xsdChildren(node).filter((child) => child.localName !== 'annotation');
		if (node.localName === 'choice') {
			if (children.length > 0) {
				const alternatives = children.slice(0, 10).map(describeAlternative).join(' | ');
				addComment(ctx, element, 'choice, pick one of: ' + alternatives + (children.length > 10 ? ' | ...' : ''));
				fillParticleChild(ctx, element, children[0], schema, depth);
			}
		} else {
			for (const child of children) {
				if (ctx.exhausted) {
					break;
				}

				fillParticleChild(ctx, element, child, schema, depth);
			}
		}

		ctx.nesting -= 1;
	}

	function setSimpleContent(ctx, element, sample, fixed) {
		if (sample.note) {
			addCommentBefore(ctx, element, sample.note);
		}

		appendText(ctx, element, fixed !== null ? fixed : sample.text);
	}

	// Fills an element whose type is given by a QName.
	function fillByTypeName(ctx, element, type, depth, fixed) {
		if (type.ns === NS_XSD) {
			// SoapCore's XmlSerializer WSDL declares DateTimeOffset as xsd:string, so only the name can tell it is a date.
			if (type.local === 'string' && fixed === null && /DateTimeOffset/i.test(element.localName)) {
				setSimpleContent(ctx, element, { text: DATE_TIME_OFFSET_SAMPLE, note: 'ISO 8601 date-time with offset' }, null);
				return;
			}

			setSimpleContent(ctx, element, { text: builtinSample(type.local), note: '' }, fixed);
			return;
		}

		const complex = ctx.model ? ctx.model.complexTypes.get(type.key) : undefined;
		if (complex) {
			if (ctx.seen.has(type.key)) {
				addComment(ctx, element, 'recursive type ' + type.local + ', not expanded further');
				return;
			}

			ctx.seen.add(type.key);
			fillComplexType(ctx, element, complex.node, complex.schema, depth);
			ctx.seen.delete(type.key);
			return;
		}

		// DataContract array types. They are usually declared, this covers the case where they are not.
		// It must run before the generic handling of the Serialization namespaces below.
		if (type.ns === NS_MS_ARRAYS && type.local.startsWith('ArrayOf') && type.local.length > 7) {
			const itemName = type.local.slice(7);
			addComment(ctx, element, 'repeatable');
			const item = createElement(ctx, element, NS_MS_ARRAYS, itemName, depth + 1);
			if (item) {
				appendText(ctx, item, builtinSample(itemName));
			}

			return;
		}

		const simple = simpleSampleForType(ctx, type, new Set());
		if (simple) {
			setSimpleContent(ctx, element, simple, fixed);
			return;
		}

		addComment(ctx, element, 'unresolved type: ' + type.local);
	}

	function expandElement(ctx, element, declaration, schema, depth) {
		let fixed = declaration.getAttribute('fixed');
		if (fixed === null) {
			fixed = declaration.getAttribute('default');
		}

		const inlineComplex = firstXsdChild(declaration, 'complexType');
		if (inlineComplex) {
			fillComplexType(ctx, element, inlineComplex, schema, depth);
			return;
		}

		const inlineSimple = firstXsdChild(declaration, 'simpleType');
		if (inlineSimple) {
			setSimpleContent(ctx, element, sampleForSimpleTypeNode(ctx, inlineSimple, new Set()), fixed);
			return;
		}

		const type = resolveQName(declaration, declaration.getAttribute('type'));
		if (type) {
			fillByTypeName(ctx, element, type, depth, fixed);
		} else {
			setSimpleContent(ctx, element, { text: 'string', note: '' }, fixed);
		}
	}

	// Builds one element declaration (global or local, possibly a reference) into the parent.
	function buildElement(ctx, parent, node, schema, isGlobal, depth) {
		if (ctx.exhausted) {
			noteTruncated(ctx, parent);
			return;
		}

		let declaration = node;
		let declarationSchema = schema;
		let global = isGlobal;
		const occurs = isGlobal ? '' : describeOccurs(node);
		const referenceValue = node.getAttribute('ref');
		if (referenceValue !== null) {
			const reference = resolveQName(node, referenceValue);
			const target = reference && ctx.model ? ctx.model.elements.get(reference.key) : undefined;
			if (!target) {
				if (occurs) {
					addComment(ctx, parent, occurs);
				}

				buildUnresolvedReference(ctx, parent, reference, depth);
				return;
			}

			declaration = target.node;
			declarationSchema = target.schema;
			global = true;
		}

		const name = declaration.getAttribute('name');
		if (!name) {
			addComment(ctx, parent, 'element without a name skipped');
			return;
		}

		const notes = [occurs, describeNillable(declaration)].filter((note) => note !== '').join(', ');
		if (notes) {
			addComment(ctx, parent, notes);
		}

		if (depth > LIMITS.maxDepth) {
			// One note per parent is enough, the siblings would only repeat it.
			if (!ctx.depthParents.has(parent)) {
				ctx.depthParents.add(parent);
				addComment(ctx, parent, 'maximum depth reached, deeper elements are not shown');
			}

			if (!ctx.depthNoted) {
				ctx.depthNoted = true;
				ctx.warnings.push('The sample stops expanding at depth ' + LIMITS.maxDepth + '.');
			}

			return;
		}

		// A global element wins over the form defaults. A local element can carry its own targetNamespace.
		const form = declaration.getAttribute('form');
		const ownNamespace = declaration.getAttribute('targetNamespace');
		let ns;
		if (global) {
			ns = declarationSchema.ns;
		} else if (ownNamespace !== null) {
			ns = ownNamespace;
		} else if (form === 'qualified' || (form !== 'unqualified' && declarationSchema.qualifiedElements)) {
			ns = declarationSchema.ns;
		} else {
			ns = '';
		}

		const element = createElement(ctx, parent, ns, name, depth);
		if (!element) {
			return;
		}

		const guardKey = global ? 'element ' + qualifiedKey(declarationSchema.ns, name) : null;
		if (guardKey !== null && ctx.seen.has(guardKey)) {
			addComment(ctx, element, 'recursive element ' + name + ', not expanded further');
			return;
		}

		if (guardKey !== null) {
			ctx.seen.add(guardKey);
		}

		expandElement(ctx, element, declaration, declarationSchema, depth);
		if (guardKey !== null) {
			ctx.seen.delete(guardKey);
		}
	}

	function buildUnresolvedReference(ctx, parent, reference, depth) {
		if (!reference) {
			addComment(ctx, parent, 'element reference skipped');
			return;
		}

		const element = createElement(ctx, parent, reference.ns, reference.local, depth);
		if (!element) {
			return;
		}

		if (isMsSerialization(reference.ns)) {
			appendText(ctx, element, builtinSample(reference.local));
		} else {
			addComment(ctx, element, 'unresolved element reference: ' + reference.local);
		}
	}

	function finishEnvelope(ctx) {
		if (ctx.exhausted) {
			noteTruncated(ctx, ctx.body);
		}

		const raw = new XMLSerializer().serializeToString(ctx.doc);
		const text = prettyPrintXml(raw);
		if (text.length > LIMITS.maxBytes) {
			return fail('The generated sample is larger than 256 KB.');
		}

		return { ok: true, text: text, warnings: ctx.warnings };
	}

	// Finds the global element declarations of the input message of an operation (document and literal style).
	// Returns { ok: true, declarations } with one { node, schema } for each part, in order, or { ok: false, error }.
	// Shared by the sample builder and the form template, so both refuse the same operations for the same reasons.
	function resolveInputElements(model, operation) {
		if (!model || !model.operations || !operation || typeof operation.name !== 'string') {
			return fail('No operation was given.');
		}

		const described = model.operations.get(operation.name);
		if (!described) {
			return fail('The WSDL does not describe the operation ' + operation.name + '.');
		}

		const message = model.messages.get(described.inputMessage);
		if (!message) {
			return fail('The WSDL has no input message for this operation.');
		}

		if (message.parts.length === 0) {
			return fail('The input message of this operation has no parts.');
		}

		for (const part of message.parts) {
			if (!part.element) {
				return fail('This operation uses rpc or encoded style, which the sample generator does not support.');
			}
		}

		const declarations = [];
		for (const part of message.parts) {
			const declaration = model.elements.get(part.element.key);
			if (!declaration) {
				return fail('The WSDL does not declare the element ' + part.element.local + '.');
			}

			declarations.push(declaration);
		}

		return { ok: true, declarations: declarations };
	}

	function buildEnvelopeCore(model, operation, version) {
		const envelopeNs = envelopeNamespace(version);
		if (!envelopeNs) {
			return fail('Unsupported SOAP version.');
		}

		const resolved = resolveInputElements(model, operation);
		if (!resolved.ok) {
			return resolved;
		}

		const ctx = createContext(model, envelopeNs);
		for (const declaration of resolved.declarations) {
			buildElement(ctx, ctx.body, declaration.node, declaration.schema, true, 1);
		}

		return finishEnvelope(ctx);
	}

	// Builds a sample request envelope for an operation of the JSON catalog.
	// Returns { ok: true, text, warnings } or { ok: false, error }. Never throws.
	function buildEnvelope(model, operation, version) {
		try {
			return buildEnvelopeCore(model, operation, version);
		} catch (error) {
			return fail('The sample could not be generated: ' + describeError(error));
		}
	}

	// Adds one element per input parameter (direction in or ref, a missing direction counts as in) of the catalog metadata
	// to the wrapper element, in the namespace of the wrapper. Output parameters are skipped. Known CLR types get a sample
	// value (sampleForClrType), anything else a comment. Returns how many input parameters were found, so the caller knows
	// whether the wrapper stayed empty.
	function addFallbackParameters(ctx, wrapper, ns, parameters) {
		if (!Array.isArray(parameters)) {
			return 0;
		}

		let found = 0;
		for (const parameter of parameters) {
			if (ctx.exhausted) {
				break;
			}

			if (!parameter || typeof parameter.name !== 'string' || parameter.name === '') {
				continue;
			}

			const direction = parameter.direction === undefined || parameter.direction === null ? 'in' : parameter.direction;
			if (direction !== 'in' && direction !== 'ref') {
				continue;
			}

			found += 1;
			// The name comes from the host. A name that is not valid XML becomes a comment. createElement does that for most
			// of them, but a browser can read a second colon as part of the name, so a colon is refused here.
			if (parameter.name.indexOf(':') !== -1) {
				addComment(ctx, wrapper, 'element skipped, the name is not valid XML: ' + parameter.name);
				continue;
			}

			const element = createElement(ctx, wrapper, ns, parameter.name, 2);
			if (!element) {
				continue;
			}

			const sample = sampleForClrType(parameter.type);
			if (sample && sample.note) {
				addCommentBefore(ctx, element, sample.note);
			}

			if (sample) {
				appendText(ctx, element, sample.text);
			} else {
				const typeName = typeof parameter.type === 'string' ? parameter.type.replace(/</g, '(').replace(/>/g, ')') : '';
				addComment(ctx, element, 'complex type ' + (typeName === '' ? 'unknown' : typeName) + ': fill in manually');
			}
		}

		return found;
	}

	// A generic envelope for when the WSDL is unavailable. The wrapper element name and namespace come from the catalog
	// (operation.name, operation.contractNamespace). When the catalog also lists the parameters (operation.parameters, see
	// the operation details of the services API) the wrapper gets one element for each of the input ones.
	function fallbackEnvelope(operation, version) {
		try {
			const envelopeNs = envelopeNamespace(version);
			if (!envelopeNs) {
				return fail('Unsupported SOAP version.');
			}

			if (!operation || typeof operation.name !== 'string' || operation.name === '') {
				return fail('No operation was given.');
			}

			const ctx = createContext(null, envelopeNs);
			const ns = typeof operation.contractNamespace === 'string' ? operation.contractNamespace : '';
			const wrapper = createElement(ctx, ctx.body, ns, operation.name, 1);
			if (!wrapper) {
				return fail('The operation name cannot be used as an XML element name.');
			}

			if (addFallbackParameters(ctx, wrapper, ns, operation.parameters) === 0) {
				addComment(ctx, wrapper, 'Add the parameters of this operation here.');
			}

			return finishEnvelope(ctx);
		} catch (error) {
			return fail('The envelope could not be generated: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// Request validation and protocol headers
	// ------------------------------------------------------------------

	function validateAction(action) {
		const value = typeof action === 'string' ? action : '';
		if (!/^[\x20-\x7E]*$/.test(value) || /["\\;]/.test(value)) {
			return fail('The SOAPAction must be printable ASCII and must not contain a double quote, a backslash or a semicolon.');
		}

		return { ok: true };
	}

	// Checks the request envelope before it is sent.
	function validateEnvelope(text, version) {
		const envelopeNs = envelopeNamespace(version);
		if (!envelopeNs) {
			return fail('Unsupported SOAP version.');
		}

		if (typeof text !== 'string' || text.trim() === '') {
			return fail('The request envelope is empty.');
		}

		if (hasDoctype(text)) {
			return fail('SOAP messages must not contain a DOCTYPE declaration.');
		}

		const parsed = parseXmlDocument(text);
		if (!parsed.doc) {
			return fail('The request envelope is not well-formed XML. ' + (parsed.detail || ''));
		}

		const root = parsed.doc.documentElement;
		if (!root || root.localName !== 'Envelope' || root.namespaceURI !== envelopeNs) {
			return fail('The root element must be Envelope in the namespace ' + envelopeNs + ' (SOAP ' + version + ').');
		}

		return { ok: true };
	}

	// Headers the UI always sets itself, in this order. SOAP 1.2 carries the action in the Content-Type.
	function protocolHeaders(version, action) {
		const value = typeof action === 'string' ? action : '';
		if (version === '1.2') {
			const actionPart = value === '' ? '' : '; action="' + value + '"';
			return [{ name: 'Content-Type', value: 'application/soap+xml; charset=utf-8' + actionPart }];
		}

		return [
			{ name: 'Content-Type', value: 'text/xml; charset=utf-8' },
			{ name: 'SOAPAction', value: '"' + value + '"' }
		];
	}

	// Parts of a header name that mark its value as a credential. A substring match on purpose: it also catches names like
	// X-Authorization, X-Session-Id, X-Csrf-Header or X-Api-Key. Over-matching only costs a masked input and a REDACTED value.
	const SENSITIVE_NAME_PARTS = Object.freeze([
		'auth', 'cookie', 'token', 'key', 'secret', 'pass', 'pwd', 'cred', 'session', 'sid',
		'jwt', 'bearer', 'signature', 'sig', 'csrf', 'xsrf'
	]);

	function isSensitiveHeader(name) {
		const lower = String(name).toLowerCase();
		return SENSITIVE_NAME_PARTS.some((part) => lower.indexOf(part) !== -1);
	}

	// Validates one custom header. usedNames is an optional Set of lower case names seen so far.
	// Returns { ok: true, name, value } or { ok: false, error }.
	function validateHeader(name, value, usedNames) {
		const trimmed = String(name === null || name === undefined ? '' : name).trim();
		if (trimmed === '') {
			return fail('The header name is required.');
		}

		if (!/^[A-Za-z0-9_-]{1,128}$/.test(trimmed)) {
			return fail('Header names may contain only letters, digits, "-" and "_" (at most 128 characters).');
		}

		const lower = trimmed.toLowerCase();
		if (OWNED_HEADERS.has(lower)) {
			return fail('The explorer sets this header itself, it cannot be overridden.');
		}

		if (FORBIDDEN_HEADERS.has(lower) || FORBIDDEN_HEADER_PREFIXES.some((prefix) => lower.startsWith(prefix))) {
			return fail('Browsers do not allow this header to be set from a page.');
		}

		if (usedNames && usedNames.has(lower)) {
			return fail('This header is listed more than once.');
		}

		const text = String(value === null || value === undefined ? '' : value);
		if (text.length > 4096 || !/^[\t\x20-\x7E]*$/.test(text)) {
			return fail('Header values must be printable ASCII and at most 4096 characters.');
		}

		return { ok: true, name: trimmed, value: text };
	}

	// ------------------------------------------------------------------
	// Copy as curl (bash and zsh only)
	// ------------------------------------------------------------------

	function shellQuote(value) {
		return "'" + String(value).replace(/'/g, "'\\''") + "'";
	}

	function hasControlCharacter(value) {
		return /[\u0000-\u001F\u007F]/.test(value);
	}

	// request: { url, headers: [{ name, value }], body }
	// Returns { ok: true, text } or { ok: false, error }. Values of sensitive headers become REDACTED.
	// The body is not redacted.
	function buildCurl(request) {
		const input = request || {};
		const url = String(input.url === null || input.url === undefined ? '' : input.url);
		const headers = Array.isArray(input.headers) ? input.headers : [];
		const body = String(input.body === null || input.body === undefined ? '' : input.body);

		if (url === '' || hasControlCharacter(url)) {
			return fail('The URL is empty or contains control characters, so no command was built.');
		}

		const lines = [];
		for (const header of headers) {
			const name = String(header && header.name !== undefined && header.name !== null ? header.name : '');
			const value = String(header && header.value !== undefined && header.value !== null ? header.value : '');
			// A tab is valid in a header value (validateHeader accepts it) and is harmless inside single quotes.
			if (name === '' || hasControlCharacter(name) || hasControlCharacter(value.replace(/\t/g, ''))) {
				return fail('A header name or value contains control characters, so no command was built.');
			}

			lines.push('  -H ' + shellQuote(name + ': ' + (isSensitiveHeader(name) ? 'REDACTED' : value)));
		}

		// Tab, carriage return and line feed are fine in a quoted here-document. Other control characters are not.
		if (/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/.test(body)) {
			return fail('The request body contains control characters that cannot be copied safely into a shell.');
		}

		const bodyLines = body.replace(/\r\n?/g, '\n').split('\n');
		const present = new Set(bodyLines);
		let delimiter = 'SOAP_EOF';
		while (present.has(delimiter)) {
			delimiter += '_X';
		}

		let content = bodyLines.join('\n');
		if (content.endsWith('\n')) {
			content = content.slice(0, -1);
		}

		const command = ['curl -X POST ' + shellQuote(url)].concat(lines, ['  --data-binary @- <<' + shellQuote(delimiter)]);
		return { ok: true, text: command.join(' \\\n') + '\n' + content + '\n' + delimiter + '\n' };
	}

	// ------------------------------------------------------------------
	// Display, link and command helpers (pure, they need no DOM and never throw)
	// ------------------------------------------------------------------

	// Strips the contract namespace from a SOAPAction. The action is returned as it is when the namespace is not
	// a prefix of it or nothing would be left.
	function shortenSoapAction(action, contractNamespace) {
		if (typeof action !== 'string') {
			return '';
		}

		if (typeof contractNamespace !== 'string' || contractNamespace === '') {
			return action;
		}

		const prefix = contractNamespace.endsWith('/') ? contractNamespace.slice(0, -1) : contractNamespace;
		if (prefix === '' || !action.startsWith(prefix)) {
			return action;
		}

		const rest = action.slice(prefix.length).replace(/^\/+/, '');
		return rest === '' ? action : rest;
	}

	// Formats a byte count with 1024 steps, for example "512 B", "1.5 KB" or "5.0 GB". Anything else than a
	// finite number that is not negative gives "0 B".
	function formatBytes(count) {
		if (typeof count !== 'number' || !Number.isFinite(count) || count < 0) {
			return '0 B';
		}

		if (count < 1024) {
			return Math.floor(count) + ' B';
		}

		let value = count / 1024;
		let unit = 1;
		while (unit < BYTE_UNITS.length - 1 && Math.round(value * 10) / 10 >= 1024) {
			value /= 1024;
			unit += 1;
		}

		return value.toFixed(1) + ' ' + BYTE_UNITS[unit];
	}

	// The hash of a deep link: #service-id, or #service-id/OperationName.
	function buildHashRoute(serviceId, operationName) {
		if (typeof serviceId !== 'string') {
			return '#';
		}

		const route = '#' + encodeURIComponent(serviceId);
		return typeof operationName === 'string' && operationName !== '' ? route + '/' + encodeURIComponent(operationName) : route;
	}

	// Reads the hash of a deep link. Returns { serviceId, operationName }, both empty when the hash is absent or
	// not valid. Only the first two segments count.
	function parseHashRoute(hash) {
		const empty = { serviceId: '', operationName: '' };
		if (typeof hash !== 'string') {
			return empty;
		}

		const segments = (hash.charAt(0) === '#' ? hash.slice(1) : hash).split('/');
		try {
			return {
				serviceId: decodeURIComponent(segments[0]),
				operationName: segments.length > 1 ? decodeURIComponent(segments[1]) : ''
			};
		} catch (error) {
			return empty;
		}
	}

	// The command that generates a .NET client from a WSDL. Only plain http and https URLs are accepted, so the text
	// can be pasted into any shell without quoting surprises ('&' is refused on purpose).
	// The control character check is explicit because engines differ on whether '$' also matches before a final line break.
	function buildSvcutilCommand(wsdlUrl) {
		if (typeof wsdlUrl !== 'string' || hasControlCharacter(wsdlUrl) || !SVCUTIL_URL.test(wsdlUrl)) {
			return fail('The WSDL URL has characters that cannot be pasted safely into a command line.');
		}

		return { ok: true, text: 'dotnet-svcutil "' + wsdlUrl + '"' };
	}

	// ------------------------------------------------------------------
	// UTF-8, Base64 and SHA-1 (plain JavaScript, so they run in every host)
	// ------------------------------------------------------------------

	const BASE64_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';

	function hexByte(value) {
		return (value < 16 ? '0' : '') + value.toString(16).toUpperCase();
	}

	function hexUnit(value) {
		return ('0000' + value.toString(16).toUpperCase()).slice(-4);
	}

	function isHighSurrogate(code) {
		return code >= 0xD800 && code <= 0xDBFF;
	}

	function isLowSurrogate(code) {
		return code >= 0xDC00 && code <= 0xDFFF;
	}

	// The UTF-8 bytes of a text, as an array of numbers. A surrogate pair is one code point and an unpaired surrogate
	// becomes U+FFFD, as it does in TextEncoder and in .NET. Anything but a string gives an empty array.
	function utf8Bytes(text) {
		const bytes = [];
		if (typeof text !== 'string') {
			return bytes;
		}

		for (let index = 0; index < text.length; index += 1) {
			let code = text.charCodeAt(index);
			if (isHighSurrogate(code)) {
				const next = index + 1 < text.length ? text.charCodeAt(index + 1) : 0;
				if (isLowSurrogate(next)) {
					code = 0x10000 + ((code - 0xD800) << 10) + (next - 0xDC00);
					index += 1;
				} else {
					code = 0xFFFD;
				}
			} else if (isLowSurrogate(code)) {
				code = 0xFFFD;
			}

			if (code < 0x80) {
				bytes.push(code);
			} else if (code < 0x800) {
				bytes.push(0xC0 | (code >> 6), 0x80 | (code & 0x3F));
			} else if (code < 0x10000) {
				bytes.push(0xE0 | (code >> 12), 0x80 | ((code >> 6) & 0x3F), 0x80 | (code & 0x3F));
			} else {
				bytes.push(0xF0 | (code >> 18), 0x80 | ((code >> 12) & 0x3F), 0x80 | ((code >> 6) & 0x3F), 0x80 | (code & 0x3F));
			}
		}

		return bytes;
	}

	// The value as a plain array of integers 0 to 255 (a Uint8Array is copied), otherwise null.
	function toByteList(value) {
		let list = null;
		if (Array.isArray(value)) {
			list = value;
		} else if (typeof Uint8Array !== 'undefined' && value instanceof Uint8Array) {
			list = Array.from(value);
		}

		if (list === null) {
			return null;
		}

		for (let index = 0; index < list.length; index += 1) {
			const item = list[index];
			if (typeof item !== 'number' || !Number.isInteger(item) || item < 0 || item > 255) {
				return null;
			}
		}

		return list;
	}

	// Standard Base64 (RFC 4648) with padding. Anything but an array of bytes gives an empty string.
	function base64Encode(bytes) {
		const list = toByteList(bytes);
		if (list === null) {
			return '';
		}

		let result = '';
		for (let index = 0; index < list.length; index += 3) {
			const hasSecond = index + 1 < list.length;
			const hasThird = index + 2 < list.length;
			const triple = (list[index] << 16) | ((hasSecond ? list[index + 1] : 0) << 8) | (hasThird ? list[index + 2] : 0);
			result += BASE64_ALPHABET.charAt((triple >> 18) & 63) + BASE64_ALPHABET.charAt((triple >> 12) & 63);
			result += hasSecond ? BASE64_ALPHABET.charAt((triple >> 6) & 63) : '=';
			result += hasThird ? BASE64_ALPHABET.charAt(triple & 63) : '=';
		}

		return result;
	}

	// SHA-1 (RFC 3174) of an array of bytes, as an array of 20 bytes. SHA-1 is only here because the WS-Security
	// UsernameToken profile defines the password digest with it. It is not used for anything else.
	function sha1Bytes(bytes) {
		const length = bytes.length;
		const paddedLength = Math.ceil((length + 9) / 64) * 64;
		const data = new Array(paddedLength);
		for (let index = 0; index < paddedLength; index += 1) {
			data[index] = index < length ? bytes[index] : 0;
		}

		data[length] = 0x80;
		const bitLength = length * 8;
		const high = Math.floor(bitLength / 4294967296);
		const low = bitLength >>> 0;
		for (let index = 0; index < 4; index += 1) {
			data[paddedLength - 8 + index] = (high >>> (24 - index * 8)) & 255;
			data[paddedLength - 4 + index] = (low >>> (24 - index * 8)) & 255;
		}

		let h0 = 0x67452301;
		let h1 = 0xEFCDAB89 | 0;
		let h2 = 0x98BADCFE | 0;
		let h3 = 0x10325476;
		let h4 = 0xC3D2E1F0 | 0;
		const words = new Array(80);
		for (let chunk = 0; chunk < paddedLength; chunk += 64) {
			for (let index = 0; index < 16; index += 1) {
				const offset = chunk + index * 4;
				words[index] = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
			}

			for (let index = 16; index < 80; index += 1) {
				const mixed = words[index - 3] ^ words[index - 8] ^ words[index - 14] ^ words[index - 16];
				words[index] = (mixed << 1) | (mixed >>> 31);
			}

			let a = h0;
			let b = h1;
			let c = h2;
			let d = h3;
			let e = h4;
			for (let index = 0; index < 80; index += 1) {
				let f;
				let k;
				if (index < 20) {
					f = (b & c) | (~b & d);
					k = 0x5A827999;
				} else if (index < 40) {
					f = b ^ c ^ d;
					k = 0x6ED9EBA1;
				} else if (index < 60) {
					f = (b & c) | (b & d) | (c & d);
					k = 0x8F1BBCDC | 0;
				} else {
					f = b ^ c ^ d;
					k = 0xCA62C1D6 | 0;
				}

				const next = (((a << 5) | (a >>> 27)) + f + e + k + words[index]) | 0;
				e = d;
				d = c;
				c = (b << 30) | (b >>> 2);
				b = a;
				a = next;
			}

			h0 = (h0 + a) | 0;
			h1 = (h1 + b) | 0;
			h2 = (h2 + c) | 0;
			h3 = (h3 + d) | 0;
			h4 = (h4 + e) | 0;
		}

		const digest = [];
		for (const word of [h0, h1, h2, h3, h4]) {
			digest.push((word >>> 24) & 255, (word >>> 16) & 255, (word >>> 8) & 255, word & 255);
		}

		return digest;
	}

	// The WS-Security UsernameToken password digest, Base64(SHA-1(nonce + created + password)), exactly as SoapCore's
	// WsMessageFilter computes it: the decoded nonce bytes, then the UTF-8 bytes of the created text, then the UTF-8
	// bytes of the password. Returns an empty string when the nonce is not an array of bytes or a text is not a string.
	function wsPasswordDigest(nonceBytes, created, password) {
		const nonce = toByteList(nonceBytes);
		if (nonce === null || typeof created !== 'string' || typeof password !== 'string') {
			return '';
		}

		return base64Encode(sha1Bytes(nonce.concat(utf8Bytes(created), utf8Bytes(password))));
	}

	// ------------------------------------------------------------------
	// Code snippets: curl, PowerShell, C# and raw HTTP (pure, they need no DOM and never throw)
	// ------------------------------------------------------------------

	const SNIPPET_KINDS = Object.freeze([
		Object.freeze({ id: 'curl', label: 'curl (bash)' }),
		Object.freeze({ id: 'powershell', label: 'PowerShell' }),
		Object.freeze({ id: 'csharp', label: 'C# (HttpClient)' }),
		Object.freeze({ id: 'http', label: 'Raw HTTP' })
	]);

	// PowerShell reads the typographic single quotes (U+2018 to U+201B) as quote characters too, so they are all escaped.
	const POWERSHELL_QUOTES = /['\u2018\u2019\u201A\u201B]/g;
	const POWERSHELL_HERE_END = /^['\u2018\u2019\u201A\u201B]@/;
	const HTTP_TOKEN = /^[!#$%&'*+.^_`|~0-9A-Za-z-]+$/;

	// Checks the request like buildCurl does and applies the redaction of sensitive header values. Returns
	// { ok: true, url, headers: [{ name, value }], body } or { ok: false, error }. Line breaks of the body become LF.
	function prepareSnippetRequest(request) {
		const url = String(request.url === null || request.url === undefined ? '' : request.url);
		const body = String(request.body === null || request.body === undefined ? '' : request.body);
		const list = Array.isArray(request.headers) ? request.headers : [];
		if (url === '' || hasControlCharacter(url)) {
			return fail('The URL is empty or contains control characters, so no snippet was built.');
		}

		const headers = [];
		for (const header of list) {
			const name = String(header && header.name !== undefined && header.name !== null ? header.name : '');
			const value = String(header && header.value !== undefined && header.value !== null ? header.value : '');
			// A tab is valid in a header value (validateHeader accepts it), the other control characters are not.
			if (name === '' || hasControlCharacter(name) || hasControlCharacter(value.replace(/\t/g, ''))) {
				return fail('A header name or value contains control characters, so no snippet was built.');
			}

			headers.push({ name: name, value: isSensitiveHeader(name) ? 'REDACTED' : value });
		}

		// Tab, carriage return and line feed are fine in a body. Other control characters are not.
		if (/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/.test(body)) {
			return fail('The request body contains control characters that cannot be copied safely into a snippet.');
		}

		return { ok: true, url: url, headers: headers, body: body.replace(/\r\n?/g, '\n') };
	}

	function isContentType(header) {
		return header.name.toLowerCase() === 'content-type';
	}

	// The first Content-Type header of the request, or null.
	function findContentType(headers) {
		for (const header of headers) {
			if (isContentType(header)) {
				return header;
			}
		}

		return null;
	}

	// A PowerShell single-quoted string. Nothing inside it is expanded, only the quote characters need escaping (doubled).
	function powershellQuote(value) {
		return "'" + String(value).replace(POWERSHELL_QUOTES, (quote) => quote + quote) + "'";
	}

	// Invoke-WebRequest script. The body is a single-quoted here-string, which expands nothing. A here-string ends at a
	// line that starts with a quote and an at sign, so a body with such a line cannot be pasted and is refused.
	function buildPowershellSnippet(prepared) {
		const body = prepared.body;
		if (body.split('\n').some((line) => POWERSHELL_HERE_END.test(line))) {
			return fail('A line of the request body starts with the characters that end a PowerShell here-string (a quote and an at sign), so no PowerShell script was built.');
		}

		const contentType = findContentType(prepared.headers);
		const others = prepared.headers.filter((header) => !isContentType(header));
		const lines = [];
		if (others.length === 0) {
			lines.push('$headers = @{}');
		} else {
			lines.push('$headers = @{');
			for (const header of others) {
				lines.push('    ' + powershellQuote(header.name) + ' = ' + powershellQuote(header.value));
			}

			lines.push('}');
		}

		// The line break after the opening and before the closing delimiter are not part of the string.
		lines.push("$body = @'", body, "'@");
		lines.push('$response = Invoke-WebRequest -Uri ' + powershellQuote(prepared.url) + ' -Method Post -Headers $headers' +
			(contentType ? ' -ContentType ' + powershellQuote(contentType.value) : '') + ' -Body $body');
		lines.push('$response.Content');
		return { ok: true, text: lines.join('\n') + '\n' };
	}

	// A C# regular string literal: backslash and double quote are escaped, and so are the characters a C# compiler
	// treats as a line break or cannot hold in a source file (control characters, NEL, LS, PS and unpaired surrogates).
	function csharpString(value) {
		const text = String(value);
		let result = '"';
		for (let index = 0; index < text.length; index += 1) {
			const character = text.charAt(index);
			const code = text.charCodeAt(index);
			if (character === '\\') {
				result += '\\\\';
			} else if (character === '"') {
				result += '\\"';
			} else if (character === '\t') {
				result += '\\t';
			} else if (isHighSurrogate(code) && index + 1 < text.length && isLowSurrogate(text.charCodeAt(index + 1))) {
				result += character + text.charAt(index + 1);
				index += 1;
			} else if (code < 0x20 || (code >= 0x7F && code <= 0x9F) || code === 0x2028 || code === 0x2029 || isHighSurrogate(code) || isLowSurrogate(code)) {
				result += '\\u' + hexUnit(code);
			} else {
				result += character;
			}
		}

		return result + '"';
	}

	// A C# raw string literal (C# 11). The delimiter is longer than the longest run of double quotes in the text, and at
	// least three. The closing delimiter has its own line at column 0, so the lines of the text are used unchanged. The
	// line break after the opening and before the closing delimiter are not part of the string.
	function csharpRawString(text) {
		let longest = 0;
		let run = 0;
		for (let index = 0; index < text.length; index += 1) {
			run = text.charAt(index) === '"' ? run + 1 : 0;
			longest = Math.max(longest, run);
		}

		const delimiter = '"'.repeat(Math.max(3, longest + 1));
		return delimiter + '\n' + text + '\n' + delimiter;
	}

	// HttpClient program (top level statements). The Content-Type header is not a request header in HttpClient, its media
	// type goes to the StringContent (the parameters, charset included, are left to it).
	function buildCsharpSnippet(prepared) {
		const lines = [
			'using System;',
			'using System.Net.Http;',
			'using System.Text;',
			'',
			'using var client = new HttpClient();',
			'using var request = new HttpRequestMessage(HttpMethod.Post, ' + csharpString(prepared.url) + ');'
		];
		for (const header of prepared.headers) {
			if (!isContentType(header)) {
				lines.push('request.Headers.TryAddWithoutValidation(' + csharpString(header.name) + ', ' + csharpString(header.value) + ');');
			}
		}

		const contentType = findContentType(prepared.headers);
		const mediaType = contentType ? contentType.value.split(';')[0].trim() : '';
		lines.push('var body = ' + csharpRawString(prepared.body) + ';');
		lines.push('request.Content = new StringContent(body, Encoding.UTF8' + (mediaType === '' ? '' : ', ' + csharpString(mediaType)) + ');');
		lines.push('using var response = await client.SendAsync(request);');
		lines.push('Console.WriteLine(await response.Content.ReadAsStringAsync());');
		return { ok: true, text: lines.join('\n') + '\n' };
	}

	// Percent encodes everything that is not visible ASCII (spaces and non ASCII characters, as UTF-8) in the path and query.
	function percentEncodeTarget(text) {
		let result = '';
		for (let index = 0; index < text.length; index += 1) {
			const code = text.charCodeAt(index);
			if (code > 0x20 && code < 0x7F) {
				result += text.charAt(index);
				continue;
			}

			let unit = text.charAt(index);
			if (isHighSurrogate(code) && index + 1 < text.length && isLowSurrogate(text.charCodeAt(index + 1))) {
				unit += text.charAt(index + 1);
				index += 1;
			}

			for (const byte of utf8Bytes(unit)) {
				result += '%' + hexByte(byte);
			}
		}

		return result;
	}

	// Splits an absolute http or https URL into the Host header value and the request target (path and query).
	// User information and the fragment are not part of an HTTP request and are dropped.
	function splitHttpUrl(url) {
		const match = /^https?:\/\/([^\/?#]*)([^#]*)/i.exec(url);
		if (!match) {
			return fail('A raw HTTP message needs an absolute http or https URL.');
		}

		const authority = match[1];
		const host = authority.slice(authority.lastIndexOf('@') + 1);
		if (!/^[\x21-\x7E]+$/.test(host)) {
			return fail('The host of the URL is empty or is not printable ASCII, so no HTTP message was built.');
		}

		const path = match[2] === '' || match[2].charAt(0) === '?' ? '/' + match[2] : match[2];
		return { ok: true, host: host, target: percentEncodeTarget(path) };
	}

	// HTTP/1.1 request message. Host and Content-Length are computed, a header of the request with one of these names is left out.
	// Lines end with LF and the text has no final line break, so Content-Length is the byte length of the body shown.
	function buildHttpSnippet(prepared) {
		const parts = splitHttpUrl(prepared.url);
		if (!parts.ok) {
			return parts;
		}

		const lines = ['POST ' + parts.target + ' HTTP/1.1', 'Host: ' + parts.host];
		for (const header of prepared.headers) {
			const lower = header.name.toLowerCase();
			if (lower === 'host' || lower === 'content-length') {
				continue;
			}

			if (!HTTP_TOKEN.test(header.name)) {
				return fail('The header name ' + shorten(header.name, 40) + ' cannot be used in an HTTP message, it must be a token (letters, digits and a few symbols, no spaces or colons).');
			}

			lines.push(header.name + ': ' + header.value);
		}

		lines.push('Content-Length: ' + utf8Bytes(prepared.body).length);
		return { ok: true, text: lines.join('\n') + '\n\n' + prepared.body };
	}

	function buildSnippetCore(kind, request) {
		if (typeof kind !== 'string' || !SNIPPET_KINDS.some((entry) => entry.id === kind)) {
			return fail('Unknown snippet kind.');
		}

		if (request === null || typeof request !== 'object' || Array.isArray(request)) {
			return fail('No request was given.');
		}

		if (kind === 'curl') {
			return buildCurl(request);
		}

		const prepared = prepareSnippetRequest(request);
		if (!prepared.ok) {
			return prepared;
		}

		switch (kind) {
			case 'powershell':
				return buildPowershellSnippet(prepared);
			case 'csharp':
				return buildCsharpSnippet(prepared);
			default:
				return buildHttpSnippet(prepared);
		}
	}

	// Builds a code snippet of the request. kind is one of the ids of snippetKinds, request is { url, headers: [{ name, value }], body }
	// as for curl.build. Returns { ok: true, text } or { ok: false, error }. Never throws. Values of sensitive headers become
	// REDACTED, the body is not redacted. The text is meant to be pasted as code, so every value is escaped for the language.
	function buildSnippet(kind, request) {
		try {
			return buildSnippetCore(kind, request);
		} catch (error) {
			return fail('The snippet could not be built: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// Authorization headers (pure, they need no DOM and never throw)
	// ------------------------------------------------------------------

	function buildAuthHeaderCore(scheme, values) {
		if (values === null || typeof values !== 'object' || Array.isArray(values)) {
			return fail('No credentials were given.');
		}

		let name = 'Authorization';
		let value;
		switch (scheme) {
			case 'bearer': {
				const token = values.token;
				if (typeof token !== 'string' || token === '') {
					return fail('The token is required.');
				}

				if (!/^[\x21-\x7E]+$/.test(token)) {
					return fail('The token must be printable ASCII without spaces.');
				}

				value = 'Bearer ' + token;
				break;
			}

			case 'basic': {
				const username = values.username;
				const password = values.password === undefined || values.password === null ? '' : values.password;
				if (typeof username !== 'string' || username === '') {
					return fail('The username is required.');
				}

				if (typeof password !== 'string') {
					return fail('The password must be text.');
				}

				if (username.indexOf(':') !== -1) {
					return fail('The username must not contain a colon, Basic authentication could not tell it from the separator.');
				}

				if (hasControlCharacter(username) || hasControlCharacter(password)) {
					return fail('The username and the password must not contain control characters.');
				}

				value = 'Basic ' + base64Encode(utf8Bytes(username + ':' + password));
				break;
			}

			case 'apiKey': {
				const keyValue = values.value;
				if (typeof values.name !== 'string') {
					return fail('The header name is required.');
				}

				if (typeof keyValue !== 'string' || keyValue.trim() === '') {
					return fail('The API key is required.');
				}

				if (!/^[\x20-\x7E]+$/.test(keyValue)) {
					return fail('The API key must be printable ASCII.');
				}

				const checked = validateHeader(values.name, keyValue);
				if (!checked.ok) {
					return checked;
				}

				name = checked.name;
				value = keyValue;
				break;
			}

			default:
				return fail('Unknown authorization scheme.');
		}

		// Every header the explorer sends is held to the same limits (4096 printable ASCII characters).
		const checkedHeader = validateHeader(name, value);
		if (!checkedHeader.ok) {
			return checkedHeader;
		}

		return { ok: true, header: { name: name, value: value } };
	}

	// Builds the authorization header of a scheme: bearer { token }, basic { username, password } or apiKey { name, value }.
	// Returns { ok: true, header: { name, value } } or { ok: false, error }. Never throws.
	function buildAuthHeader(scheme, values) {
		try {
			return buildAuthHeaderCore(scheme, values);
		} catch (error) {
			return fail('The authorization header could not be built: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// WS-Security UsernameToken
	// ------------------------------------------------------------------

	const WSS_NONCE_BYTES = 16;
	const WSS_CREATED = /^\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])T(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\dZ$/;

	// Text that cannot be written in an XML 1.0 document: unpaired surrogates, U+FFFE and U+FFFF (control characters are
	// refused separately).
	function hasInvalidXmlCharacter(text) {
		for (let index = 0; index < text.length; index += 1) {
			const code = text.charCodeAt(index);
			if (isHighSurrogate(code) && index + 1 < text.length && isLowSurrogate(text.charCodeAt(index + 1))) {
				index += 1;
			} else if (isHighSurrogate(code) || isLowSurrogate(code) || code === 0xFFFE || code === 0xFFFF) {
				return true;
			}
		}

		return false;
	}

	// An empty string when the value is acceptable, otherwise the problem.
	function wsTextProblem(label, value, maxLength) {
		if (typeof value !== 'string') {
			return 'The ' + label + ' must be text.';
		}

		if (value.length > maxLength) {
			return 'The ' + label + ' is longer than ' + maxLength + ' characters.';
		}

		if (hasControlCharacter(value)) {
			return 'The ' + label + ' contains control characters.';
		}

		return hasInvalidXmlCharacter(value) ? 'The ' + label + ' contains characters that XML cannot hold.' : '';
	}

	// The two pure helpers that assemble the header. Every value goes through escapeXml, every attribute value through escapeAttribute.
	function wsAttribute(name, value) {
		return ' ' + name + '="' + escapeAttribute(value) + '"';
	}

	function wsElement(name, attributes, text) {
		return '<' + name + attributes + '>' + escapeXml(text) + '</' + name + '>';
	}

	function buildWsSecurityHeaderCore(options, nowIso, nonceBytes) {
		if (options === null || typeof options !== 'object' || Array.isArray(options)) {
			return fail('No WS-Security options were given.');
		}

		const digest = options.passwordType === 'digest';
		if (!digest && options.passwordType !== 'text') {
			return fail('The password type must be "text" or "digest".');
		}

		const username = options.username;
		if (typeof username !== 'string' || username.trim() === '') {
			return fail('The username is required.');
		}

		const password = options.password === undefined || options.password === null ? '' : options.password;
		const problem = wsTextProblem('username', username, 256) || wsTextProblem('password', password, 1024);
		if (problem !== '') {
			return fail(problem);
		}

		if (digest && password === '') {
			return fail('A password is required for the digest type.');
		}

		// SoapCore rejects a digest token that has only one of Nonce and Created, and computes the digest over both.
		const withNonce = digest || options.includeNonce === true;
		const withCreated = digest || options.includeCreated === true;
		const nonce = withNonce ? toByteList(nonceBytes) : null;
		if (withNonce && (nonce === null || nonce.length !== WSS_NONCE_BYTES)) {
			return fail('The nonce must be ' + WSS_NONCE_BYTES + ' bytes.');
		}

		if (withCreated && (typeof nowIso !== 'string' || !WSS_CREATED.test(nowIso))) {
			return fail('The creation time must look like 2025-01-31T12:00:00Z (UTC, without fractions of a second).');
		}

		const parts = [
			'<wsse:Security' + wsAttribute('xmlns:wsse', NS_WSSE) + wsAttribute('xmlns:wsu', NS_WSU) + '>',
			'<wsse:UsernameToken' + wsAttribute('wsu:Id', 'UsernameToken-1') + '>',
			wsElement('wsse:Username', '', username)
		];

		// SoapCore accepts a token without a Password element, so an empty password leaves it out.
		if (password !== '') {
			const text = options.redact === true ? 'REDACTED' : (digest ? wsPasswordDigest(nonce, nowIso, password) : password);
			parts.push(wsElement('wsse:Password', wsAttribute('Type', digest ? WSS_PASSWORD_DIGEST : WSS_PASSWORD_TEXT), text));
		}

		if (withNonce) {
			parts.push(wsElement('wsse:Nonce', wsAttribute('EncodingType', WSS_BASE64_BINARY), base64Encode(nonce)));
		}

		if (withCreated) {
			parts.push(wsElement('wsu:Created', '', nowIso));
		}

		parts.push('</wsse:UsernameToken>', '</wsse:Security>');
		return { ok: true, xml: parts.join('') };
	}

	// Builds the wsse:Security header element (one line of XML, both prefixes declared on the element) with a UsernameToken.
	// options: { username, password, passwordType: 'text' | 'digest', includeNonce, includeCreated, redact }.
	// nowIso is the creation time (UTC, like 2025-01-31T12:00:00Z) and nonceBytes 16 random bytes, both of them are used by
	// the digest type (which always includes Nonce and Created, as SoapCore requires) and when they are included.
	// With redact the Password text is REDACTED and no digest is computed. Returns { ok: true, xml } or { ok: false, error }.
	function buildWsSecurityHeader(options, nowIso, nonceBytes) {
		try {
			return buildWsSecurityHeaderCore(options, nowIso, nonceBytes);
		} catch (error) {
			return fail('The WS-Security header could not be built: ' + describeError(error));
		}
	}

	function applyWsSecurityCore(envelopeText, securityXml, version) {
		const envelopeNs = envelopeNamespace(version);
		if (!envelopeNs) {
			return fail('Unsupported SOAP version.');
		}

		const envelope = parseXmlDocument(envelopeText);
		if (!envelope.doc) {
			return fail('The request envelope could not be read. ' + envelope.error);
		}

		const root = envelope.doc.documentElement;
		if (!root || root.localName !== 'Envelope' || root.namespaceURI !== envelopeNs) {
			return fail('The root element must be Envelope in the namespace ' + envelopeNs + ' (SOAP ' + version + ').');
		}

		const security = parseXmlDocument(securityXml);
		if (!security.doc) {
			return fail('The security header could not be read. ' + security.error);
		}

		const securityRoot = security.doc.documentElement;
		if (!securityRoot || securityRoot.localName !== 'Security' || securityRoot.namespaceURI !== NS_WSSE) {
			return fail('The security header must be a Security element in the WS-Security namespace.');
		}

		// A missing Header is created as the first child of the Envelope, with the prefix the Envelope itself uses.
		let header = directChild(root, [envelopeNs], 'Header');
		if (!header) {
			header = envelope.doc.createElementNS(envelopeNs, root.prefix ? root.prefix + ':Header' : 'Header');
			root.insertBefore(header, root.firstChild);
		}

		// A Security element of an earlier call is replaced, not duplicated.
		for (const child of childElements(header)) {
			if (child.namespaceURI === NS_WSSE && child.localName === 'Security') {
				header.removeChild(child);
			}
		}

		// Appending a node of another document adopts it, the Security element does not need to be copied.
		header.appendChild(securityRoot);

		// The serialiser leaves the XML declaration out. It is put back when the envelope had one.
		let raw = new XMLSerializer().serializeToString(envelope.doc);
		const declaration = /^\uFEFF?\s*(<\?xml\s[^>]*?\?>)/.exec(envelopeText);
		if (declaration && !/^\s*<\?xml\s/.test(raw)) {
			raw = declaration[1] + raw;
		}

		return { ok: true, text: prettyPrintXml(raw) };
	}

	// Puts a wsse:Security header element (see buildWsSecurityHeader) into the SOAP Header of an envelope. A Header that
	// is missing is created, a WS-Security header that is already there is replaced, and the Body is not touched.
	// version is '1.1' or '1.2'. Returns { ok: true, text } (pretty printed) or { ok: false, error }. Needs a DOM. Never throws.
	function applyWsSecurity(envelopeText, securityXml, version) {
		try {
			return applyWsSecurityCore(envelopeText, securityXml, version);
		} catch (error) {
			return fail('The security header could not be added: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// Sample values for CLR types (the type names of the operation details of the services API)
	// ------------------------------------------------------------------

	const CLR_SAMPLES = new Map([
		['System.String', 'string'],
		['System.Boolean', 'true'],
		['System.Int16', '1'],
		['System.Int32', '1'],
		['System.Int64', '1'],
		['System.UInt16', '1'],
		['System.UInt32', '1'],
		['System.UInt64', '1'],
		['System.Byte', '1'],
		['System.SByte', '1'],
		['System.Single', '1.5'],
		['System.Double', '1.5'],
		['System.Decimal', '1.5'],
		['System.DateTime', SAMPLE_DATE_TIME],
		['System.DateTimeOffset', DATE_TIME_OFFSET_SAMPLE],
		['System.TimeSpan', 'PT1H'],
		['System.Guid', GUID_SAMPLE],
		['System.Char', '65'],
		['System.Byte[]', 'AAAA'],
		['System.Uri', 'https://example.com']
	]);

	const CLR_LIST_TYPES = new Set([
		'System.Collections.Generic.List',
		'System.Collections.Generic.IList',
		'System.Collections.Generic.IEnumerable',
		'System.Collections.Generic.ICollection'
	]);

	// True when the generic arguments hold more than one type (a comma outside any nested angle brackets).
	function hasTopLevelComma(text) {
		let depth = 0;
		for (let index = 0; index < text.length; index += 1) {
			const character = text.charAt(index);
			if (character === '<') {
				depth += 1;
			} else if (character === '>') {
				depth -= 1;
			} else if (character === ',' && depth === 0) {
				return true;
			}
		}

		return false;
	}

	function repeatableSample(sample) {
		return sample ? { text: sample.text, note: 'repeatable' } : null;
	}

	function clrSample(typeName, depth) {
		if (depth > 8 || typeName === '') {
			return null;
		}

		const direct = CLR_SAMPLES.get(typeName);
		if (direct !== undefined) {
			return { text: direct, note: '' };
		}

		if (typeName.endsWith('[]')) {
			return repeatableSample(clrSample(typeName.slice(0, -2).trim(), depth + 1));
		}

		const open = typeName.indexOf('<');
		if (open > 0 && typeName.endsWith('>')) {
			const generic = typeName.slice(0, open).trim();
			const argument = typeName.slice(open + 1, -1).trim();
			if (hasTopLevelComma(argument)) {
				return null;
			}

			if (generic === 'System.Nullable') {
				return clrSample(argument, depth + 1);
			}

			if (CLR_LIST_TYPES.has(generic)) {
				return repeatableSample(clrSample(argument, depth + 1));
			}
		}

		return null;
	}

	// A sample value for a CLR type name as the services API writes it (full names without an assembly, for example
	// System.Int32, System.Nullable<System.Int32>, System.String[] or System.Collections.Generic.List<System.Guid>).
	// Returns { text, note } or null for a type that is not known (complex types, enumerations, dictionaries and so on).
	// A nullable type gives the sample of the type behind it. An array or a list gives the sample of its item type with the
	// note "repeatable". System.Byte[] is a single value (Base64), not a list. Never throws.
	function sampleForClrType(typeName) {
		try {
			if (typeof typeName !== 'string' || typeName.length > 512) {
				return null;
			}

			return clrSample(typeName.trim(), 0);
		} catch (error) {
			return null;
		}
	}

	// ------------------------------------------------------------------
	// Typed values for the form editor (pure, they need no DOM and never throw)
	// ------------------------------------------------------------------

	// A TypeInfo is { xsd, enumValues, pattern }: the built-in XML Schema type a value derives from (one of KNOWN_XSD_TYPES,
	// everything else is 'string'), the allowed values (null without an enumeration, at most MAX_ENUM_VALUES) and the pattern
	// facet (null without one). The form template (buildFormTemplate) produces them, the three functions below consume them.
	const KNOWN_XSD_TYPES = new Set([
		'string', 'boolean', 'decimal', 'float', 'double', 'int', 'integer', 'long', 'short', 'byte',
		'unsignedInt', 'unsignedLong', 'unsignedShort', 'unsignedByte', 'positiveInteger', 'nonNegativeInteger',
		'negativeInteger', 'nonPositiveInteger', 'dateTime', 'date', 'time', 'duration', 'base64Binary', 'hexBinary',
		'anyURI', 'guid', 'char'
	]);

	const MAX_ENUM_VALUES = 200;
	// A pattern is only run against values up to this length, so a hostile pattern cannot freeze the page on a long value.
	const MAX_PATTERN_INPUT = 5000;

	// Lower and upper bound (as text, null = unbounded) of the integer types. The comparison works on the digits, so it
	// has no precision limit and needs no BigInt.
	const INTEGER_RANGES = new Map([
		['integer', [null, null]],
		['long', ['-9223372036854775808', '9223372036854775807']],
		['int', ['-2147483648', '2147483647']],
		['short', ['-32768', '32767']],
		['byte', ['-128', '127']],
		['unsignedLong', ['0', '18446744073709551615']],
		['unsignedInt', ['0', '4294967295']],
		['unsignedShort', ['0', '65535']],
		['unsignedByte', ['0', '255']],
		['nonNegativeInteger', ['0', null]],
		['positiveInteger', ['1', null]],
		['nonPositiveInteger', [null, '0']],
		['negativeInteger', [null, '-1']]
	]);

	const INTEGER_PATTERN = /^([+-]?)(\d+)$/;
	const DECIMAL_PATTERN = /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$/;
	const FLOAT_PATTERN = /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/;
	const DATE_TIME_PATTERN = /^(-?)(\d{4,})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?(Z|[+-]\d{2}:\d{2})?$/;
	const DATE_PATTERN = /^(-?)(\d{4,})-(\d{2})-(\d{2})(Z|[+-]\d{2}:\d{2})?$/;
	const TIME_PATTERN = /^(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?(Z|[+-]\d{2}:\d{2})?$/;
	const DURATION_PATTERN = /^-?P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?(T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$/;
	const BASE64_PATTERN = /^(?:[A-Za-z0-9+\/]{4})*(?:[A-Za-z0-9+\/]{2}==|[A-Za-z0-9+\/]{3}=)?$/;
	const HEX_BINARY_PATTERN = /^(?:[0-9A-Fa-f]{2})*$/;
	const GUID_PATTERN = /^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$/;
	const URI_PATTERN = /^[^\s\u0000-\u001F\u007F-\u009F]*$/;
	// Constructs of the XML Schema pattern language that mean something else (or nothing) in JavaScript: the multi character
	// escapes, the block escapes and the subtraction of character classes. Such a pattern is not used to validate.
	const XSD_ONLY_PATTERN = /\\[iIcC]|\\[pP]\{Is|\[[^\]]*-\[/;

	function isPlainObject(value) {
		return value !== null && typeof value === 'object' && !Array.isArray(value);
	}

	// XML whitespace only (space, tab, carriage return and line feed), which is what the whitespace facet collapses.
	function trimXmlSpace(text) {
		return text.replace(/^[ \t\r\n]+|[ \t\r\n]+$/g, '');
	}

	// The enumeration of a TypeInfo as an array of strings, or null when there is none.
	function allowedValuesOf(info) {
		if (!info || !Array.isArray(info.enumValues) || info.enumValues.length === 0) {
			return null;
		}

		return info.enumValues.map((value) => String(value));
	}

	function kindResult(kind, inputMode, placeholder, pattern) {
		return { kind: kind, inputMode: inputMode, placeholder: placeholder, pattern: pattern };
	}

	function inputKindCore(typeInfo) {
		const info = isPlainObject(typeInfo) ? typeInfo : null;
		const pattern = info && typeof info.pattern === 'string' ? info.pattern : null;
		if (allowedValuesOf(info) !== null) {
			return kindResult('enum', 'text', '', pattern);
		}

		const xsd = info && typeof info.xsd === 'string' ? info.xsd : '';
		if (INTEGER_RANGES.has(xsd)) {
			return kindResult('integer', 'numeric', '0', pattern);
		}

		switch (xsd) {
			case 'decimal':
			case 'float':
			case 'double':
				return kindResult('number', 'decimal', '0.0', pattern);
			case 'boolean':
				return kindResult('boolean', 'text', '', pattern);
			case 'dateTime':
				return kindResult('datetime', 'text', SAMPLE_DATE_TIME, pattern);
			case 'date':
				return kindResult('date', 'text', SAMPLE_DATE, pattern);
			case 'time':
				return kindResult('time', 'text', '12:00:00', pattern);
			case 'duration':
				return kindResult('duration', 'text', 'PT1H', pattern);
			case 'base64Binary':
				return kindResult('base64', 'text', 'AAAA', pattern);
			case 'guid':
				return kindResult('guid', 'text', GUID_SAMPLE, pattern);
			default:
				return kindResult('text', 'text', '', pattern);
		}
	}

	// How a form should edit a value of a type (a TypeInfo of the form template). Returns { kind, inputMode, placeholder, pattern }.
	// kind is text, integer, number, boolean, enum, datetime, date, time, duration, base64 or guid. inputMode (the HTML
	// inputmode hint) is numeric, decimal or text. pattern repeats the pattern facet of the type and is only a hint.
	// Anything that is not a TypeInfo gives the kind text. Never throws.
	function inputKindForType(typeInfo) {
		try {
			return inputKindCore(typeInfo);
		} catch (error) {
			return kindResult('text', 'text', '', null);
		}
	}

	// The sample value of a type: the first enumeration value, otherwise the sample the envelope builder uses for the built-in
	// type. Anything that is not a TypeInfo gives 'string'. Never throws.
	function sampleValueForType(typeInfo) {
		try {
			if (isPlainObject(typeInfo)) {
				const values = allowedValuesOf(typeInfo);
				if (values !== null) {
					return values[0];
				}

				if (typeof typeInfo.xsd === 'string') {
					return builtinSample(typeInfo.xsd);
				}
			}
		} catch (error) {
			// The sample below is the answer for anything that cannot be read.
		}

		return 'string';
	}

	function parseInteger(text) {
		const match = INTEGER_PATTERN.exec(text);
		if (!match) {
			return null;
		}

		const magnitude = match[2].replace(/^0+(?=\d)/, '');
		return { negative: match[1] === '-' && magnitude !== '0', magnitude: magnitude };
	}

	// -1, 0 or 1 for a compared with b (both from parseInteger).
	function compareIntegers(a, b) {
		if (a.negative !== b.negative) {
			return a.negative ? -1 : 1;
		}

		let result = 0;
		if (a.magnitude.length !== b.magnitude.length) {
			result = a.magnitude.length < b.magnitude.length ? -1 : 1;
		} else if (a.magnitude !== b.magnitude) {
			result = a.magnitude < b.magnitude ? -1 : 1;
		}

		return a.negative ? -result : result;
	}

	// An empty string when the text is an integer of the type, otherwise the problem. The label names the type in the message.
	function integerProblem(label, rangeName, text) {
		const value = parseInteger(text);
		if (value === null) {
			return 'Not a whole number, for example 42.';
		}

		const range = INTEGER_RANGES.get(rangeName);
		const lower = range[0] === null ? null : parseInteger(range[0]);
		const upper = range[1] === null ? null : parseInteger(range[1]);
		if ((lower !== null && compareIntegers(value, lower) < 0) || (upper !== null && compareIntegers(value, upper) > 0)) {
			if (lower !== null && upper !== null) {
				return 'Out of range: ' + label + ' allows ' + range[0] + ' to ' + range[1] + '.';
			}

			return lower !== null ? 'Must be ' + range[0] + ' or greater.' : 'Must be ' + range[1] + ' or less.';
		}

		return '';
	}

	// Days of a month. Only the last four digits of the year count: 10000 is a multiple of 400, so they decide the leap year.
	function daysInMonth(yearDigits, month) {
		if (month === 2) {
			const year = Number(yearDigits.slice(-4));
			return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0 ? 29 : 28;
		}

		return month === 4 || month === 6 || month === 9 || month === 11 ? 30 : 31;
	}

	function dateProblem(yearDigits, monthText, dayText) {
		const month = Number(monthText);
		const day = Number(dayText);
		if (/^0+$/.test(yearDigits) || month < 1 || month > 12 || day < 1 || day > daysInMonth(yearDigits, month)) {
			return 'The date is not valid.';
		}

		return '';
	}

	// 24:00:00 is the end of the day and is valid only with zero minutes, seconds and fraction.
	function timeProblem(hourText, minuteText, secondText, fraction) {
		const hour = Number(hourText);
		const minute = Number(minuteText);
		const second = Number(secondText);
		const endOfDay = hour === 24 && minute === 0 && second === 0 && (fraction === undefined || /^0+$/.test(fraction));
		if (!endOfDay && (hour > 23 || minute > 59 || second > 59)) {
			return 'The time is not valid.';
		}

		return '';
	}

	// The offset of a time zone is Z or a signed hh:mm up to 14:00.
	function zoneProblem(zone) {
		if (zone === undefined || zone === 'Z') {
			return '';
		}

		const hours = Number(zone.slice(1, 3));
		const minutes = Number(zone.slice(4, 6));
		return hours > 14 || minutes > 59 || (hours === 14 && minutes !== 0) ? 'The time zone offset is not valid.' : '';
	}

	function dateTimeProblem(text) {
		const match = DATE_TIME_PATTERN.exec(text);
		if (!match) {
			return 'Not a valid date and time, for example 2025-01-01T12:00:00Z.';
		}

		return dateProblem(match[2], match[3], match[4]) || timeProblem(match[5], match[6], match[7], match[8]) || zoneProblem(match[9]);
	}

	function dateOnlyProblem(text) {
		const match = DATE_PATTERN.exec(text);
		if (!match) {
			return 'Not a valid date, for example 2025-01-01.';
		}

		return dateProblem(match[2], match[3], match[4]) || zoneProblem(match[5]);
	}

	function timeOnlyProblem(text) {
		const match = TIME_PATTERN.exec(text);
		if (!match) {
			return 'Not a valid time, for example 12:00:00.';
		}

		return timeProblem(match[1], match[2], match[3], match[4]) || zoneProblem(match[5]);
	}

	function durationProblem(text) {
		const match = DURATION_PATTERN.exec(text);
		const dateParts = match ? [match[1], match[2], match[3]] : [];
		const timeParts = match ? [match[5], match[6], match[7]] : [];
		const hasDate = dateParts.some((part) => part !== undefined);
		const hasTime = timeParts.some((part) => part !== undefined);
		// P and PT alone are not durations, and neither is a T without a time part after it.
		if (!match || (!hasDate && !hasTime) || (match[4] !== undefined && !hasTime)) {
			return 'Not a valid duration, for example PT1H or P1DT2H30M.';
		}

		return '';
	}

	function floatProblem(text) {
		return FLOAT_PATTERN.test(text) || text === 'INF' || text === '-INF' || text === 'NaN' ? '' : 'Not a valid number, for example 1.5 or 1E3.';
	}

	// An empty string when the text has the lexical form of the built-in type, otherwise the problem. Unknown types are accepted.
	function lexicalProblem(xsd, text) {
		if (INTEGER_RANGES.has(xsd)) {
			return integerProblem('xs:' + xsd, xsd, text);
		}

		switch (xsd) {
			case 'char':
				// The Microsoft char type travels as the number of the UTF-16 unit.
				return integerProblem('char', 'unsignedShort', text);
			case 'decimal':
				return DECIMAL_PATTERN.test(text) ? '' : 'Not a valid decimal number, for example 1.5.';
			case 'float':
			case 'double':
				return floatProblem(text);
			case 'boolean':
				return text === 'true' || text === 'false' || text === '1' || text === '0' ? '' : 'Not a valid boolean, use true or false.';
			case 'dateTime':
				return dateTimeProblem(text);
			case 'date':
				return dateOnlyProblem(text);
			case 'time':
				return timeOnlyProblem(text);
			case 'duration':
				return durationProblem(text);
			case 'base64Binary':
				// Whitespace is allowed inside Base64 text. Only the structure is checked: the alphabet, the length and the padding.
				return BASE64_PATTERN.test(text.replace(/[ \t\r\n]+/g, '')) ? '' : 'Not valid Base64 text.';
			case 'hexBinary':
				return HEX_BINARY_PATTERN.test(text) ? '' : 'Not valid hexadecimal text, it needs an even number of digits 0-9 and A-F.';
			case 'guid':
				return GUID_PATTERN.test(text) ? '' : 'Not a valid GUID, for example 00000000-0000-0000-0000-000000000000.';
			case 'anyURI':
				return URI_PATTERN.test(text) ? '' : 'A URI must not contain spaces or control characters.';
			default:
				return '';
		}
	}

	// Compiles the pattern facet of a type to a regular expression that must match the whole value, or returns null when it
	// cannot be used. Category escapes (\p{L}) need the u flag. A pattern the engine rejects, or one that uses constructs of the
	// XML Schema pattern language that JavaScript reads differently, is not used.
	function compilePattern(source) {
		if (typeof source !== 'string' || XSD_ONLY_PATTERN.test(source)) {
			return null;
		}

		const body = '^(?:' + source + ')$';
		try {
			return new RegExp(body, 'u');
		} catch (error) {
			// Without the u flag a category escape would silently mean something else.
			if (/\\[pP]/.test(source)) {
				return null;
			}
		}

		try {
			return new RegExp(body);
		} catch (error) {
			return null;
		}
	}

	function validateXsdValueCore(typeInfo, text) {
		if (typeof text !== 'string') {
			return fail('The value must be text.');
		}

		const info = isPlainObject(typeInfo) ? typeInfo : null;
		const xsd = info && typeof info.xsd === 'string' ? info.xsd : 'string';
		const allowed = allowedValuesOf(info);
		// The whitespace around a number, a boolean or a date is not part of the value. A string keeps it.
		const collapses = xsd !== 'string' && KNOWN_XSD_TYPES.has(xsd);
		const value = collapses ? trimXmlSpace(text) : text;
		if (allowed !== null) {
			return allowed.indexOf(text) !== -1 || allowed.indexOf(value) !== -1 ? { ok: true } : fail('The value is not one of the allowed values.');
		}

		const problem = lexicalProblem(xsd, value);
		if (problem !== '') {
			return fail(problem);
		}

		const pattern = info && typeof info.pattern === 'string' && value.length <= MAX_PATTERN_INPUT ? compilePattern(info.pattern) : null;
		if (pattern !== null && !pattern.test(value)) {
			return fail('Does not match the pattern');
		}

		return { ok: true };
	}

	// Checks the lexical form of a text against a type (a TypeInfo of the form template): the range of the integer types, the
	// shape of numbers, booleans, dates, durations, binary values, GUIDs and URIs, then the enumeration or the pattern facet.
	// Whitespace around the value is ignored for everything but strings. Returns { ok: true } or { ok: false, error }, where
	// error is a short sentence. A type that is not a TypeInfo, and string types, accept every text. Never throws.
	function validateXsdValue(typeInfo, text) {
		try {
			return validateXsdValueCore(typeInfo, text);
		} catch (error) {
			return fail('The value could not be checked: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// XML tokenizer and text search (pure, they need no DOM and never throw)
	// ------------------------------------------------------------------

	const DEFAULT_HIGHLIGHT_CHARS = 262144;
	const MAX_SEARCH_RANGES = 1000;
	const MAX_SEARCH_QUERY = 200;
	const XML_NAME_START = /[A-Za-z_:\u00C0-\uFFFF]/;

	function isXmlSpaceCode(code) {
		return code === 32 || code === 9 || code === 10 || code === 13;
	}

	// A character of a name inside a tag: anything but white space and the characters that structure a tag.
	function isTagNameCode(code) {
		return !isXmlSpaceCode(code) && code !== 61 && code !== 60 && code !== 62 && code !== 47 && code !== 34 && code !== 39;
	}

	// Splits XML text into tokens, tolerating anything. Every function that reads a construct returns the index after it,
	// which is always greater than the index it started at, so the loop ends. The tokens cover the text without gaps.
	function tokenizeXmlText(text) {
		const tokens = [];
		const length = text.length;

		function emit(type, start, end) {
			if (end <= start) {
				return;
			}

			const piece = text.slice(start, end);
			const last = tokens.length > 0 ? tokens[tokens.length - 1] : null;
			if (type === 'text' && last !== null && last.type === 'text') {
				last.text += piece;
			} else {
				tokens.push({ type: type, text: piece });
			}
		}

		// A construct that ends with a terminator and runs to the end of the text when there is none.
		function readUntil(type, start, terminator, from) {
			const found = text.indexOf(terminator, from);
			const stop = found === -1 ? length : found + terminator.length;
			emit(type, start, stop);
			return stop;
		}

		// <!DOCTYPE ...> with an optional internal subset in square brackets. Quotes and brackets are tracked, so the
		// first closing angle bracket outside of both ends it. Best effort: anything else that starts with <! is read the same way.
		function readDoctype(start) {
			let depth = 0;
			let quote = 0;
			for (let index = start + 2; index < length; index += 1) {
				const code = text.charCodeAt(index);
				if (quote !== 0) {
					quote = code === quote ? 0 : quote;
				} else if (code === 34 || code === 39) {
					quote = code;
				} else if (code === 91) {
					depth += 1;
				} else if (code === 93) {
					depth = depth > 0 ? depth - 1 : 0;
				} else if (code === 62 && depth === 0) {
					emit('doctype', start, index + 1);
					return index + 1;
				}
			}

			emit('doctype', start, length);
			return length;
		}

		// The inside of a start or end tag, after its opening angle bracket. The first name is the element name, the others are
		// attribute names. A new angle bracket ends the tag without being consumed: the tag was never closed.
		function readTag(from) {
			let index = from;
			let expectName = true;
			while (index < length) {
				const code = text.charCodeAt(index);
				if (isXmlSpaceCode(code)) {
					let end = index + 1;
					while (end < length && isXmlSpaceCode(text.charCodeAt(end))) {
						end += 1;
					}

					emit('text', index, end);
					index = end;
				} else if (code === 62) {
					emit('punct', index, index + 1);
					return index + 1;
				} else if (code === 47 && text.charCodeAt(index + 1) === 62) {
					emit('punct', index, index + 2);
					return index + 2;
				} else if (code === 60) {
					return index;
				} else if (code === 61 || code === 47) {
					emit('punct', index, index + 1);
					index += 1;
				} else if (code === 34 || code === 39) {
					const close = text.indexOf(text.charAt(index), index + 1);
					const stop = close === -1 ? length : close + 1;
					emit('string', index, stop);
					index = stop;
				} else {
					let end = index + 1;
					while (end < length && isTagNameCode(text.charCodeAt(end))) {
						end += 1;
					}

					emit(expectName ? 'tag' : 'attr', index, end);
					expectName = false;
					index = end;
				}
			}

			return length;
		}

		function readMarkup(start) {
			if (text.startsWith('<!--', start)) {
				return readUntil('comment', start, '-->', start + 4);
			}

			if (text.startsWith('<![CDATA[', start)) {
				return readUntil('cdata', start, ']]>', start + 9);
			}

			if (text.startsWith('<?', start)) {
				return readUntil('decl', start, '?>', start + 2);
			}

			if (text.startsWith('<!', start)) {
				return readDoctype(start);
			}

			if (text.startsWith('</', start)) {
				emit('punct', start, start + 2);
				return readTag(start + 2);
			}

			if (start + 1 < length && XML_NAME_START.test(text.charAt(start + 1))) {
				emit('punct', start, start + 1);
				return readTag(start + 1);
			}

			// A lone angle bracket is character data.
			emit('text', start, start + 1);
			return start + 1;
		}

		let index = 0;
		while (index < length) {
			if (text.charCodeAt(index) === 60) {
				index = readMarkup(index);
			} else {
				const next = text.indexOf('<', index);
				const stop = next === -1 ? length : next;
				emit('text', index, stop);
				index = stop;
			}
		}

		return tokens;
	}

	// Splits XML text into tokens for syntax highlighting. Returns { ok: true, tokens: [{ type, text }] } where the texts of all
	// tokens joined are exactly the input, or { ok: false, error } when the text is longer than maxChars (default 262144).
	// Token types: decl (<?...?>), comment, cdata, doctype, punct (< </ > /> = and a stray slash), tag (the element name with its
	// prefix), attr (an attribute name), string (an attribute value with its quotes), text (character data and the white space
	// between and inside tags). The input does not need to be well-formed: a construct that never ends becomes one token of its
	// type up to the end of the text, and a tag that never closes ends where the next angle bracket starts. Never throws.
	function tokenizeXml(text, maxChars) {
		try {
			if (typeof text !== 'string') {
				return fail('The text must be a string.');
			}

			const limit = typeof maxChars === 'number' && Number.isFinite(maxChars) && maxChars > 0 ? Math.floor(maxChars) : DEFAULT_HIGHLIGHT_CHARS;
			if (text.length > limit) {
				return fail('The text is too large to highlight.');
			}

			return { ok: true, tokens: tokenizeXmlText(text) };
		} catch (error) {
			return fail('The text could not be tokenized: ' + describeError(error));
		}
	}

	// The text in lower case, one UTF-16 unit for each unit, so that a position in the result is a position in the original.
	// Every character (a surrogate pair is one) is lowered on its own. A character whose lower case has another length (U+0130,
	// the capital I with a dot, becomes two units in JavaScript) is kept as it is. This is a simple case mapping, not a full
	// case folding: the German sharp s does not match "ss".
	function lowerKeepingLength(text) {
		let result = '';
		for (let index = 0; index < text.length; index += 1) {
			let unit = text.charAt(index);
			if (isHighSurrogate(text.charCodeAt(index)) && index + 1 < text.length && isLowSurrogate(text.charCodeAt(index + 1))) {
				unit += text.charAt(index + 1);
				index += 1;
			}

			const lower = unit.toLowerCase();
			result += lower.length === unit.length ? lower : unit;
		}

		return result;
	}

	function findMatchesCore(text, query, options) {
		if (typeof text !== 'string' || typeof query !== 'string') {
			return fail('The text and the search text must be strings.');
		}

		if (query === '') {
			return { ok: true, ranges: [], truncated: false };
		}

		if (query.length > MAX_SEARCH_QUERY) {
			return fail('The search text is longer than ' + MAX_SEARCH_QUERY + ' characters.');
		}

		const sensitive = isPlainObject(options) && options.caseSensitive === true;
		const haystack = sensitive ? text : lowerKeepingLength(text);
		const needle = sensitive ? query : lowerKeepingLength(query);
		const ranges = [];
		let truncated = false;
		let from = 0;
		for (;;) {
			const found = haystack.indexOf(needle, from);
			if (found === -1) {
				break;
			}

			if (ranges.length >= MAX_SEARCH_RANGES) {
				truncated = true;
				break;
			}

			from = found + query.length;
			ranges.push({ start: found, end: from });
		}

		return { ok: true, ranges: ranges, truncated: truncated };
	}

	// Finds the literal text (not a regular expression) in a text. options: { caseSensitive } (default false). Returns
	// { ok: true, ranges: [{ start, end }], truncated } with the matches in order and without overlaps (positions are UTF-16
	// units of the original text), or { ok: false, error }. At most 1000 ranges are returned, truncated tells there were more.
	// An empty query finds nothing, a query longer than 200 characters or an argument that is not a string is an error.
	// The case insensitive search maps every character to lower case on its own, see lowerKeepingLength. Never throws.
	function findMatches(text, query, options) {
		try {
			return findMatchesCore(text, query, options);
		} catch (error) {
			return fail('The search failed: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// Form template: the schema walker of the sample builder, emitting data instead of DOM
	// ------------------------------------------------------------------
	//
	// buildFormTemplate follows the same rules as buildEnvelope (the same element, complex type, extension, sequence, all and
	// choice handling, the same limits and the same recursion guards), but the result is a tree of plain objects:
	//   Node = { name, ns, kind: 'simple' | 'complex', type: TypeInfo | null, children, attributes, min, max,
	//            nillable, choiceGroup, anyContent, truncated, note }
	//   Attr = { name, ns, type: TypeInfo, required, fixed }
	// The differences from the sample are on purpose: every alternative of a choice is listed (with the same choiceGroup),
	// an element that cannot occur (maxOccurs 0) is left out, and where the sample writes a comment the node carries a note and,
	// for content the form cannot edit, anyContent. An element or attribute whose name is not a valid XML name is left out and
	// named in the warnings.

	const TEMPLATE_WARNING_LIMIT = 50;
	const TEMPLATE_NOTE_LIMIT = 600;
	const TEMPLATE_VALUE_LIMIT = 500;
	// An XML name without a colon (NCName), for the Basic Multilingual Plane. Names outside of it are refused.
	const XML_LOCAL_NAME = /^[A-Za-z_\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD][A-Za-z0-9_.\-\u00B7\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u037D\u037F-\u1FFF\u200C\u200D\u203F\u2040\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD]*$/;

	function makeTypeInfo(xsd, enumValues, pattern) {
		return { xsd: xsd, enumValues: enumValues, pattern: pattern };
	}

	function builtinTypeInfo(local) {
		return makeTypeInfo(KNOWN_XSD_TYPES.has(local) ? local : 'string', null, null);
	}

	function isValidLocalName(name) {
		return typeof name === 'string' && name.length <= 256 && XML_LOCAL_NAME.test(name);
	}

	function createTemplateContext(model) {
		return {
			model: model,
			seen: new Set(),
			nodes: 0,
			nesting: 0,
			groups: 0,
			exhausted: false,
			truncationNoted: false,
			depthNoted: false,
			warnings: []
		};
	}

	function templateWarning(ctx, message) {
		if (ctx.warnings.length < TEMPLATE_WARNING_LIMIT && ctx.warnings.indexOf(message) === -1) {
			ctx.warnings.push(message);
		}
	}

	function newTemplateNode(name, ns) {
		return {
			name: name,
			ns: ns,
			kind: 'simple',
			type: makeTypeInfo('string', null, null),
			children: [],
			attributes: [],
			min: 1,
			max: 1,
			nillable: false,
			choiceGroup: null,
			anyContent: false,
			truncated: false,
			note: ''
		};
	}

	function addTemplateNote(node, text) {
		const clean = String(text).slice(0, TEMPLATE_VALUE_LIMIT);
		if (clean === '' || node.note.indexOf(clean) !== -1 || node.note.length + clean.length + 2 > TEMPLATE_NOTE_LIMIT) {
			return;
		}

		node.note = node.note === '' ? clean : node.note + '; ' + clean;
	}

	// Content the form cannot model: the node says so and the text says why.
	function flagTemplateNode(node, text) {
		node.anyContent = true;
		addTemplateNote(node, text);
	}

	function makeComplex(node) {
		node.kind = 'complex';
		node.type = null;
	}

	// Makes the node a simple one. result is { info, note, unsupported } from the simple type functions.
	function makeSimple(node, result, valueNote) {
		node.kind = 'simple';
		node.type = result.info;
		if (valueNote !== '') {
			addTemplateNote(node, valueNote);
		}

		if (result.note !== '') {
			if (result.unsupported) {
				flagTemplateNode(node, result.note);
			} else {
				addTemplateNote(node, result.note);
			}
		}
	}

	function simpleResult(info, note, unsupported) {
		return { info: info, note: note, unsupported: unsupported };
	}

	function markTemplateTruncated(ctx, node) {
		node.truncated = true;
		if (!ctx.truncationNoted) {
			ctx.truncationNoted = true;
			templateWarning(ctx, 'The form was truncated because it reached the size limit (2,000 elements).');
		}
	}

	// Counts a new node. Returns false, and marks the parent as truncated, when the limit is reached.
	function reserveTemplateNode(ctx, parent) {
		if (!ctx.exhausted && ctx.nodes >= LIMITS.maxNodes) {
			ctx.exhausted = true;
		}

		if (ctx.exhausted) {
			markTemplateTruncated(ctx, parent);
			return false;
		}

		ctx.nodes += 1;
		return true;
	}

	// minOccurs and maxOccurs: a whole number, or unbounded for the maximum (null). Anything unreadable is the default, 1.
	function readMinOccurs(node) {
		const value = (node.getAttribute('minOccurs') || '').trim();
		return /^\d{1,9}$/.test(value) ? Number(value) : 1;
	}

	function readMaxOccurs(node) {
		const value = (node.getAttribute('maxOccurs') || '').trim();
		if (value === 'unbounded' || /^\d{10,}$/.test(value)) {
			return null;
		}

		return /^\d{1,9}$/.test(value) ? Number(value) : 1;
	}

	// The pattern facets of a restriction as one expression (several patterns of one restriction are alternatives), or null.
	function patternFacet(restriction) {
		const patterns = [];
		for (const child of xsdChildren(restriction)) {
			if (child.localName === 'pattern' && child.getAttribute('value') !== null) {
				patterns.push(child.getAttribute('value'));
			}
		}

		if (patterns.length === 0) {
			return null;
		}

		return patterns.length === 1 ? patterns[0] : patterns.map((pattern) => '(?:' + pattern + ')').join('|');
	}

	// The type of a xs:simpleType element, following restrictions down to a built-in type like sampleForSimpleTypeNode does.
	// Returns { info, note, unsupported }. A list or union is a string with an unsupported note.
	function simpleInfoForNode(ctx, node, guard) {
		for (const child of xsdChildren(node)) {
			if (child.localName === 'restriction') {
				const baseName = resolveQName(child, child.getAttribute('base'));
				const inlineType = firstXsdChild(child, 'simpleType');
				let base = null;
				if (baseName) {
					base = simpleInfoForType(ctx, baseName, guard);
				} else if (inlineType) {
					base = simpleInfoForNode(ctx, inlineType, guard);
				}

				const inherited = base || simpleResult(makeTypeInfo('string', null, null), '', false);
				const values = enumerationValues(child);
				const pattern = patternFacet(child);
				let note = inherited.note;
				let enumValues = inherited.info.enumValues;
				if (values.length > MAX_ENUM_VALUES) {
					// Part of an enumeration would reject valid values, so none of it is offered.
					enumValues = null;
					note = 'enumeration with more than ' + MAX_ENUM_VALUES + ' values';
				} else if (values.length > 0) {
					enumValues = values;
				}

				const info = makeTypeInfo(inherited.info.xsd, enumValues, pattern !== null ? pattern : inherited.info.pattern);
				return simpleResult(info, note, inherited.unsupported);
			}

			if (child.localName === 'list' || child.localName === 'union') {
				return simpleResult(makeTypeInfo('string', null, null), UNSUPPORTED + 'simpleType/' + child.localName, true);
			}
		}

		return simpleResult(makeTypeInfo('string', null, null), '', false);
	}

	// The type of a QName when it is a simple type, otherwise null. Same order as simpleSampleForType.
	function simpleInfoForType(ctx, type, guard) {
		if (type.ns === NS_XSD) {
			return simpleResult(builtinTypeInfo(type.local), '', false);
		}

		if (type.local === 'guid') {
			return simpleResult(makeTypeInfo('guid', null, null), '', false);
		}

		if (type.local === 'char' && isMsSerialization(type.ns)) {
			return simpleResult(makeTypeInfo('char', null, null), '', false);
		}

		const declared = ctx.model ? ctx.model.simpleTypes.get(type.key) : undefined;
		if (declared) {
			if (guard.has(type.key) || guard.size > LIMITS.maxDepth) {
				return simpleResult(makeTypeInfo('string', null, null), 'recursive simple type: ' + type.local, false);
			}

			guard.add(type.key);
			const result = simpleInfoForNode(ctx, declared.node, guard);
			guard.delete(type.key);
			return result;
		}

		if (isMsSerialization(type.ns)) {
			return simpleResult(builtinTypeInfo(type.local), '', false);
		}

		return null;
	}

	function attributeTypeInfo(ctx, declaration) {
		const inlineType = firstXsdChild(declaration, 'simpleType');
		if (inlineType) {
			return simpleInfoForNode(ctx, inlineType, new Set()).info;
		}

		const type = resolveQName(declaration, declaration.getAttribute('type'));
		const result = type ? simpleInfoForType(ctx, type, new Set()) : null;
		return result ? result.info : makeTypeInfo('string', null, null);
	}

	// Adds one attribute declaration to the node, like buildAttribute. Prohibited attributes and the plumbing of the
	// serializers are left out, so are names that are not valid XML names.
	function addTemplateAttribute(ctx, tNode, node, schema) {
		if (node.getAttribute('use') === 'prohibited') {
			return;
		}

		let declaration = node;
		let name = node.getAttribute('name');
		let ns = '';
		const referenceValue = node.getAttribute('ref');
		if (referenceValue !== null) {
			const reference = resolveQName(node, referenceValue);
			if (!reference) {
				return;
			}

			if (reference.ns === NS_XML) {
				name = reference.local;
				ns = NS_XML;
			} else if (isMsSerialization(reference.ns)) {
				return;
			} else {
				const target = ctx.model ? ctx.model.attributes.get(reference.key) : undefined;
				if (!target) {
					addTemplateNote(tNode, 'unresolved attribute reference: ' + reference.local);
					return;
				}

				declaration = target.node;
				name = declaration.getAttribute('name');
				ns = target.schema.ns;
			}
		} else {
			const form = node.getAttribute('form');
			const qualified = form === 'qualified' || (form !== 'unqualified' && schema.qualifiedAttributes);
			ns = qualified ? schema.ns : '';
		}

		if (!name || name === 'xmlns' || name.startsWith('xmlns:')) {
			return;
		}

		if (!isValidLocalName(name)) {
			templateWarning(ctx, 'An attribute was skipped, the name is not valid XML: ' + shorten(name, 40));
			return;
		}

		const fixed = declaration.getAttribute('fixed');
		tNode.attributes.push({
			name: name,
			ns: ns,
			type: attributeTypeInfo(ctx, declaration),
			required: node.getAttribute('use') === 'required',
			fixed: fixed === null ? null : fixed.slice(0, TEMPLATE_VALUE_LIMIT)
		});
	}

	// The content of a complex type, like fillComplexType. optional is true inside an optional group.
	function fillTemplateComplexType(ctx, tNode, node, schema, depth, optional) {
		for (const child of xsdChildren(node)) {
			if (ctx.exhausted) {
				markTemplateTruncated(ctx, tNode);
				return;
			}

			switch (child.localName) {
				case 'sequence':
				case 'all':
				case 'choice':
					fillTemplateParticle(ctx, tNode, child, schema, depth, optional, false);
					break;
				case 'complexContent':
					fillTemplateComplexContent(ctx, tNode, child, schema, depth, optional);
					break;
				case 'attribute':
					addTemplateAttribute(ctx, tNode, child, schema);
					break;
				case 'simpleContent':
				case 'group':
				case 'attributeGroup':
					flagTemplateNode(tNode, UNSUPPORTED + child.localName);
					break;
				default:
					break;
			}
		}
	}

	function fillTemplateComplexContent(ctx, tNode, node, schema, depth, optional) {
		for (const child of xsdChildren(node)) {
			if (ctx.exhausted) {
				markTemplateTruncated(ctx, tNode);
				return;
			}

			if (child.localName === 'extension') {
				const base = resolveQName(child, child.getAttribute('base'));
				if (base && base.ns !== NS_XSD) {
					const declared = ctx.model ? ctx.model.complexTypes.get(base.key) : undefined;
					if (declared && !ctx.seen.has(base.key)) {
						// Members of the base type come first, as they do on the wire.
						ctx.seen.add(base.key);
						fillTemplateComplexType(ctx, tNode, declared.node, declared.schema, depth, optional);
						ctx.seen.delete(base.key);
					} else if (declared) {
						tNode.truncated = true;
						addTemplateNote(tNode, 'recursive base type: ' + base.local);
					} else {
						flagTemplateNode(tNode, 'unresolved base type: ' + base.local);
					}
				}

				fillTemplateComplexType(ctx, tNode, child, schema, depth, optional);
			} else if (child.localName === 'restriction') {
				flagTemplateNode(tNode, UNSUPPORTED + 'complexContent/restriction');
			}
		}
	}

	// repeat is true inside a group that repeats: its elements can occur any number of times.
	function fillTemplateParticleChild(ctx, tNode, child, schema, depth, optional, repeat) {
		switch (child.localName) {
			case 'element':
				buildTemplateElement(ctx, tNode, child, schema, false, depth + 1, { optional: optional, repeat: repeat, choiceGroup: null });
				break;
			case 'sequence':
			case 'all':
			case 'choice':
				fillTemplateParticle(ctx, tNode, child, schema, depth, optional, repeat);
				break;
			case 'any':
				flagTemplateNode(tNode, 'any element is allowed here');
				break;
			case 'group':
				flagTemplateNode(tNode, UNSUPPORTED + 'group');
				break;
			default:
				break;
		}
	}

	// A sequence, all or choice, like fillParticle. A choice lists all of its element alternatives with one choiceGroup. A
	// group that repeats, or an alternative that is not a single element, cannot be edited as a form and is flagged.
	// In an optional group the elements are optional, and in a group that repeats their maximum is unbounded.
	function fillTemplateParticle(ctx, tNode, node, schema, depth, optional, repeatOuter) {
		if (ctx.nesting >= LIMITS.maxParticleNesting) {
			tNode.truncated = true;
			addTemplateNote(tNode, 'schema nesting limit reached');
			return;
		}

		const maxOccurs = readMaxOccurs(node);
		if (maxOccurs === 0) {
			return;
		}

		ctx.nesting += 1;
		const groupOptional = optional || readMinOccurs(node) === 0;
		const repeat = repeatOuter || maxOccurs === null || maxOccurs > 1;
		if (maxOccurs === null || maxOccurs > 1) {
			flagTemplateNode(tNode, 'repeating ' + node.localName);
		}

		const children = xsdChildren(node).filter((child) => child.localName !== 'annotation');
		if (node.localName === 'choice') {
			const elements = children.filter((child) => child.localName === 'element');
			// One alternative is not a choice, the element is a plain one. When another kind of alternative is
			// possible as well, none of the elements is required.
			const group = elements.length > 1 ? 'c' + (ctx.groups += 1) : null;
			const alternativeOptional = groupOptional || elements.length < children.length;
			for (const child of children) {
				if (ctx.exhausted) {
					markTemplateTruncated(ctx, tNode);
					break;
				}

				if (child.localName === 'element') {
					buildTemplateElement(ctx, tNode, child, schema, false, depth + 1, { optional: alternativeOptional, repeat: repeat, choiceGroup: group });
				} else {
					flagTemplateNode(tNode, 'choice alternative that is not a single element: ' + child.localName);
				}
			}
		} else {
			for (const child of children) {
				if (ctx.exhausted) {
					markTemplateTruncated(ctx, tNode);
					break;
				}

				fillTemplateParticleChild(ctx, tNode, child, schema, depth, groupOptional, repeat);
			}
		}

		ctx.nesting -= 1;
	}

	// Fills a node whose type is given by a QName, like fillByTypeName. fixed is the fixed or default value (or null),
	// valueNote the note that tells it.
	function fillTemplateByTypeName(ctx, tNode, type, depth, fixed, valueNote, optional) {
		if (type.ns === NS_XSD) {
			// SoapCore's XmlSerializer WSDL declares DateTimeOffset as xsd:string, so only the name can tell it is a date.
			if (type.local === 'string' && fixed === null && /DateTimeOffset/i.test(tNode.name)) {
				makeSimple(tNode, simpleResult(makeTypeInfo('dateTime', null, null), 'ISO 8601 date-time with offset', false), valueNote);
				return;
			}

			makeSimple(tNode, simpleResult(builtinTypeInfo(type.local), '', false), valueNote);
			if (type.local === 'anyType') {
				flagTemplateNode(tNode, 'anyType: any content is allowed');
			}

			return;
		}

		const complex = ctx.model ? ctx.model.complexTypes.get(type.key) : undefined;
		if (complex) {
			makeComplex(tNode);
			if (ctx.seen.has(type.key)) {
				tNode.truncated = true;
				addTemplateNote(tNode, 'recursive type ' + type.local + ', not expanded further');
				return;
			}

			ctx.seen.add(type.key);
			fillTemplateComplexType(ctx, tNode, complex.node, complex.schema, depth, optional);
			ctx.seen.delete(type.key);
			return;
		}

		// DataContract array types. They are usually declared, this covers the case where they are not.
		// It must run before the generic handling of the Serialization namespaces below.
		if (type.ns === NS_MS_ARRAYS && type.local.startsWith('ArrayOf') && type.local.length > 7) {
			makeComplex(tNode);
			const itemName = type.local.slice(7);
			if (isValidLocalName(itemName) && reserveTemplateNode(ctx, tNode)) {
				const item = newTemplateNode(itemName, NS_MS_ARRAYS);
				item.type = builtinTypeInfo(itemName);
				item.min = 0;
				item.max = null;
				tNode.children.push(item);
			}

			return;
		}

		const simple = simpleInfoForType(ctx, type, new Set());
		if (simple) {
			makeSimple(tNode, simple, valueNote);
			return;
		}

		makeComplex(tNode);
		flagTemplateNode(tNode, 'unresolved type: ' + type.local);
	}

	// Fills a node from its element declaration, like expandElement.
	function expandTemplateElement(ctx, tNode, declaration, schema, depth, optional) {
		let fixed = declaration.getAttribute('fixed');
		let valueNote = fixed === null ? '' : 'fixed: ' + fixed.slice(0, TEMPLATE_VALUE_LIMIT);
		if (fixed === null) {
			fixed = declaration.getAttribute('default');
			valueNote = fixed === null ? '' : 'default: ' + fixed.slice(0, TEMPLATE_VALUE_LIMIT);
		}

		const inlineComplex = firstXsdChild(declaration, 'complexType');
		if (inlineComplex) {
			makeComplex(tNode);
			fillTemplateComplexType(ctx, tNode, inlineComplex, schema, depth, optional);
			return;
		}

		const inlineSimple = firstXsdChild(declaration, 'simpleType');
		if (inlineSimple) {
			makeSimple(tNode, simpleInfoForNode(ctx, inlineSimple, new Set()), valueNote);
			return;
		}

		const type = resolveQName(declaration, declaration.getAttribute('type'));
		if (type) {
			fillTemplateByTypeName(ctx, tNode, type, depth, fixed, valueNote, optional);
		} else {
			makeSimple(tNode, simpleResult(makeTypeInfo('string', null, null), '', false), valueNote);
		}
	}

	// The occurrence of a node: the element of the choice or sequence it is in, then its own minOccurs and maxOccurs.
	function applyTemplateOccurs(tNode, node, isGlobal, flags) {
		if (!isGlobal) {
			tNode.min = flags.optional ? 0 : readMinOccurs(node);
			tNode.max = flags.repeat ? null : readMaxOccurs(node);
		} else if (flags.optional) {
			tNode.min = 0;
		}

		tNode.choiceGroup = flags.choiceGroup;
	}

	// An element reference that does not point to a declared global element. The Serialization namespaces of the
	// DataContract serializer declare simple elements (string, int, guid ...) that WSDL does not repeat.
	function buildTemplateUnresolvedReference(ctx, parent, node, reference, flags) {
		if (!reference) {
			addTemplateNote(parent, 'element reference skipped');
			return;
		}

		if (!isValidLocalName(reference.local)) {
			templateWarning(ctx, 'An element was skipped, the name is not valid XML: ' + shorten(reference.local, 40));
			return;
		}

		if (readMaxOccurs(node) === 0 || !reserveTemplateNode(ctx, parent)) {
			return;
		}

		const tNode = newTemplateNode(reference.local, reference.ns);
		applyTemplateOccurs(tNode, node, false, flags);
		if (isMsSerialization(reference.ns)) {
			tNode.type = builtinTypeInfo(reference.local);
		} else {
			makeComplex(tNode);
			flagTemplateNode(tNode, 'unresolved element reference: ' + reference.local);
		}

		parent.children.push(tNode);
	}

	// Builds one element declaration (global or local, possibly a reference) into the parent node, like buildElement.
	// flags: { optional, choiceGroup }.
	function buildTemplateElement(ctx, parent, node, schema, isGlobal, depth, flags) {
		if (ctx.exhausted) {
			markTemplateTruncated(ctx, parent);
			return;
		}

		let declaration = node;
		let declarationSchema = schema;
		let global = isGlobal;
		const referenceValue = node.getAttribute('ref');
		if (referenceValue !== null) {
			const reference = resolveQName(node, referenceValue);
			const target = reference && ctx.model ? ctx.model.elements.get(reference.key) : undefined;
			if (!target) {
				buildTemplateUnresolvedReference(ctx, parent, node, reference, flags);
				return;
			}

			declaration = target.node;
			declarationSchema = target.schema;
			global = true;
		}

		const name = declaration.getAttribute('name');
		if (!name) {
			addTemplateNote(parent, 'element without a name skipped');
			return;
		}

		if (!isGlobal && readMaxOccurs(node) === 0) {
			return;
		}

		if (depth > LIMITS.maxDepth) {
			parent.truncated = true;
			addTemplateNote(parent, 'maximum depth reached, deeper elements are not shown');
			if (!ctx.depthNoted) {
				ctx.depthNoted = true;
				templateWarning(ctx, 'The form stops expanding at depth ' + LIMITS.maxDepth + '.');
			}

			return;
		}

		// A global element wins over the form defaults. A local element can carry its own targetNamespace.
		const form = declaration.getAttribute('form');
		const ownNamespace = declaration.getAttribute('targetNamespace');
		let ns;
		if (global) {
			ns = declarationSchema.ns;
		} else if (ownNamespace !== null) {
			ns = ownNamespace;
		} else if (form === 'qualified' || (form !== 'unqualified' && declarationSchema.qualifiedElements)) {
			ns = declarationSchema.ns;
		} else {
			ns = '';
		}

		if (!isValidLocalName(name)) {
			templateWarning(ctx, 'An element was skipped, the name is not valid XML: ' + shorten(name, 40));
			return;
		}

		if (!reserveTemplateNode(ctx, parent)) {
			return;
		}

		const tNode = newTemplateNode(name, ns);
		applyTemplateOccurs(tNode, node, isGlobal, flags);
		const nillable = declaration.getAttribute('nillable');
		tNode.nillable = nillable === 'true' || nillable === '1';
		parent.children.push(tNode);

		const guardKey = global ? 'element ' + qualifiedKey(declarationSchema.ns, name) : null;
		if (guardKey !== null && ctx.seen.has(guardKey)) {
			makeComplex(tNode);
			tNode.truncated = true;
			addTemplateNote(tNode, 'recursive element ' + name + ', not expanded further');
			return;
		}

		if (guardKey !== null) {
			ctx.seen.add(guardKey);
		}

		expandTemplateElement(ctx, tNode, declaration, declarationSchema, depth, flags.optional);
		if (guardKey !== null) {
			ctx.seen.delete(guardKey);
		}
	}

	function buildFormTemplateCore(model, operation, version) {
		if (!envelopeNamespace(version)) {
			return fail('Unsupported SOAP version.');
		}

		const resolved = resolveInputElements(model, operation);
		if (!resolved.ok) {
			return resolved;
		}

		const ctx = createTemplateContext(model);
		const root = newTemplateNode('', '');
		for (const declaration of resolved.declarations) {
			buildTemplateElement(ctx, root, declaration.node, declaration.schema, true, 1, { optional: false, repeat: false, choiceGroup: null });
		}

		return { ok: true, template: { operationName: operation.name, bodyParts: root.children, warnings: ctx.warnings } };
	}

	// Builds the data a form is made from, for an operation of the JSON catalog: { operationName, bodyParts, warnings }, where
	// bodyParts are the elements of the SOAP Body (document and literal style, like buildEnvelope, which also gives the same
	// errors) as nodes of the tree described above, and warnings tells what was cut (the size and depth limits) or skipped.
	// A node has: name and ns (the local name and namespace of the element, ns is '' when unqualified), kind (simple or complex),
	// type (a TypeInfo for a simple node, null for a complex one), children (in order, empty for a simple node), attributes,
	// min and max (minOccurs and maxOccurs, max is null when unbounded), nillable, choiceGroup (the same text for the
	// alternatives of one choice, otherwise null), anyContent (true when the type allows xs:any or has a construct the form cannot edit),
	// truncated (true when the node was not expanded completely) and note (empty or a short reason, it starts with "fixed: " or
	// "default: " when the declaration has such a value). Needs a DOM, because the model holds the schema as DOM nodes. Never throws.
	function buildFormTemplate(model, operation, version) {
		try {
			return buildFormTemplateCore(model, operation, version);
		} catch (error) {
			return fail('The form could not be generated: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// WSDL outline
	// ------------------------------------------------------------------

	// The binding extensions of WSDL 1.1: the namespace tells the SOAP version.
	const NS_WSDL_SOAP11 = NS_WSDL + 'soap/';
	const NS_WSDL_SOAP12 = NS_WSDL + 'soap12/';
	const OUTLINE_LIST_LIMIT = 500;
	const OUTLINE_TEXT_LIMIT = 300;

	function soapVersionOf(ns) {
		if (ns === NS_WSDL_SOAP11) {
			return '1.1';
		}

		return ns === NS_WSDL_SOAP12 ? '1.2' : '';
	}

	// Plain text from the document: no control characters, and not longer than OUTLINE_TEXT_LIMIT. It is data, never markup.
	function outlineText(value) {
		const text = String(value === null || value === undefined ? '' : value).replace(/[\u0000-\u001F\u007F]/g, ' ');
		return text.length > OUTLINE_TEXT_LIMIT ? text.slice(0, OUTLINE_TEXT_LIMIT - 3) + '...' : text;
	}

	// A reference such as tns:Name as the local name, without the prefix.
	function outlineLocal(value) {
		const text = outlineText(value);
		return text.slice(text.lastIndexOf(':') + 1);
	}

	function createOutlineContext() {
		return { warnings: [], capped: new Set(), references: false };
	}

	// Adds an item to a list unless the list is full. The first time a kind of list overflows it is mentioned in the warnings.
	function addCapped(ctx, list, item, label) {
		if (list.length >= OUTLINE_LIST_LIMIT) {
			if (!ctx.capped.has(label)) {
				ctx.capped.add(label);
				ctx.warnings.push('The list of ' + label + ' was cut at ' + OUTLINE_LIST_LIMIT + ' items.');
			}

			return false;
		}

		list.push(item);
		return true;
	}

	// The enumeration values of a simple type: its restriction, or the restriction inside a list.
	function enumerationCount(simpleType) {
		let current = simpleType;
		for (let level = 0; level < 4 && current; level += 1) {
			let next = null;
			for (const child of xsdChildren(current)) {
				if (child.localName === 'restriction') {
					return enumerationValues(child).length;
				}

				if (child.localName === 'list') {
					next = firstXsdChild(child, 'simpleType');
				}
			}

			current = next;
		}

		return 0;
	}

	function outlineTypes(ctx, typesNode, types) {
		for (const schema of xsdChildren(typesNode)) {
			if (schema.localName !== 'schema') {
				continue;
			}

			for (const child of xsdChildren(schema)) {
				if (child.localName === 'import' || child.localName === 'include' || child.localName === 'redefine') {
					// An import of a namespace without a location refers to a schema that is already part of the document.
					ctx.references = ctx.references || child.getAttribute('schemaLocation') !== null;
					continue;
				}

				const name = child.getAttribute('name');
				if (!name) {
					continue;
				}

				if (child.localName === 'element') {
					addCapped(ctx, types.elements, outlineText(name), 'elements');
				} else if (child.localName === 'complexType') {
					addCapped(ctx, types.complexTypes, outlineText(name), 'complex types');
				} else if (child.localName === 'simpleType') {
					addCapped(ctx, types.simpleTypes, { name: outlineText(name), enumCount: enumerationCount(child) }, 'simple types');
				}
			}
		}
	}

	function outlineMessage(ctx, node) {
		const message = { name: outlineText(node.getAttribute('name')), parts: [] };
		for (const child of childElements(node)) {
			if (child.namespaceURI === NS_WSDL && child.localName === 'part') {
				addCapped(ctx, message.parts, {
					name: outlineText(child.getAttribute('name')),
					element: outlineLocal(child.getAttribute('element')),
					type: outlineLocal(child.getAttribute('type'))
				}, 'message parts');
			}
		}

		return message;
	}

	function outlinePortType(ctx, node) {
		const portType = { name: outlineText(node.getAttribute('name')), operations: [] };
		for (const child of childElements(node)) {
			if (child.namespaceURI !== NS_WSDL || child.localName !== 'operation') {
				continue;
			}

			const operation = { name: outlineText(child.getAttribute('name')), input: '', output: '', faults: [] };
			for (const part of childElements(child)) {
				if (part.namespaceURI !== NS_WSDL) {
					continue;
				}

				if (part.localName === 'input' && operation.input === '') {
					operation.input = outlineLocal(part.getAttribute('message'));
				} else if (part.localName === 'output' && operation.output === '') {
					operation.output = outlineLocal(part.getAttribute('message'));
				} else if (part.localName === 'fault') {
					// The message of the fault tells more than its name, which is often the same.
					const fault = part.getAttribute('message') !== null ? outlineLocal(part.getAttribute('message')) : outlineText(part.getAttribute('name'));
					addCapped(ctx, operation.faults, fault, 'faults');
				}
			}

			addCapped(ctx, portType.operations, operation, 'port type operations');
		}

		return portType;
	}

	function outlineBinding(ctx, node) {
		let soapVersion = '';
		let transport = '';
		let style = '';
		const operations = [];
		for (const child of childElements(node)) {
			const version = soapVersionOf(child.namespaceURI);
			if (version !== '' && child.localName === 'binding' && soapVersion === '') {
				soapVersion = version;
				transport = outlineText(child.getAttribute('transport'));
				style = outlineText(child.getAttribute('style'));
			} else if (child.namespaceURI === NS_WSDL && child.localName === 'operation') {
				const operation = { name: outlineText(child.getAttribute('name')), soapAction: '' };
				for (const inner of childElements(child)) {
					if (inner.localName === 'operation' && soapVersionOf(inner.namespaceURI) !== '') {
						operation.soapAction = outlineText(inner.getAttribute('soapAction'));
						break;
					}
				}

				addCapped(ctx, operations, operation, 'binding operations');
			}
		}

		return {
			name: outlineText(node.getAttribute('name')),
			type: outlineLocal(node.getAttribute('type')),
			transport: transport,
			style: style,
			soapVersion: soapVersion,
			operations: operations
		};
	}

	// bindingVersions maps the name of a binding to its SOAP version, for a port whose address does not say it.
	function outlineService(ctx, node, bindingVersions) {
		const service = { name: outlineText(node.getAttribute('name')), ports: [] };
		for (const child of childElements(node)) {
			if (child.namespaceURI !== NS_WSDL || child.localName !== 'port') {
				continue;
			}

			const port = { name: outlineText(child.getAttribute('name')), binding: outlineLocal(child.getAttribute('binding')), address: '', soapVersion: '' };
			for (const extension of childElements(child)) {
				if (extension.localName === 'address' && extension.getAttribute('location') !== null) {
					port.address = outlineText(extension.getAttribute('location'));
					port.soapVersion = soapVersionOf(extension.namespaceURI);
					break;
				}
			}

			if (port.soapVersion === '' && bindingVersions.has(port.binding)) {
				port.soapVersion = bindingVersions.get(port.binding);
			}

			addCapped(ctx, service.ports, port, 'ports');
		}

		return service;
	}

	function buildWsdlOutlineCore(text) {
		const parsed = parseXmlDocument(text);
		if (!parsed.doc) {
			return fail(parsed.error);
		}

		const root = parsed.doc.documentElement;
		if (!root || root.namespaceURI !== NS_WSDL || root.localName !== 'definitions') {
			return fail('The document is not a WSDL 1.1 definitions element.');
		}

		const ctx = createOutlineContext();
		const outline = {
			targetNamespace: outlineText(root.getAttribute('targetNamespace')),
			services: [],
			bindings: [],
			portTypes: [],
			messages: [],
			types: { elements: [], complexTypes: [], simpleTypes: [] },
			warnings: ctx.warnings
		};
		const bindingVersions = new Map();
		const services = [];
		for (const section of childElements(root)) {
			if (section.namespaceURI !== NS_WSDL) {
				continue;
			}

			switch (section.localName) {
				case 'types':
					outlineTypes(ctx, section, outline.types);
					break;
				case 'message':
					addCapped(ctx, outline.messages, outlineMessage(ctx, section), 'messages');
					break;
				case 'portType':
					addCapped(ctx, outline.portTypes, outlinePortType(ctx, section), 'port types');
					break;
				case 'binding': {
					const binding = outlineBinding(ctx, section);
					bindingVersions.set(binding.name, binding.soapVersion);
					addCapped(ctx, outline.bindings, binding, 'bindings');
					break;
				}

				case 'service':
					services.push(section);
					break;
				case 'import':
					ctx.references = ctx.references || section.getAttribute('location') !== null;
					break;
				default:
					break;
			}
		}

		// A service can come before the bindings it uses, so the services are read last.
		for (const section of services) {
			addCapped(ctx, outline.services, outlineService(ctx, section, bindingVersions), 'services');
		}

		if (ctx.references) {
			ctx.warnings.push('The WSDL refers to other documents by location (wsdl:import, xs:import or xs:include). What they define is not listed.');
		}

		return { ok: true, outline: outline };
	}

	// Summarizes a WSDL for display: { targetNamespace, services: [{ name, ports: [{ name, binding, address, soapVersion }] }],
	// bindings: [{ name, type, transport, style, soapVersion, operations: [{ name, soapAction }] }], portTypes: [{ name,
	// operations: [{ name, input, output, faults }] }], messages: [{ name, parts: [{ name, element, type }] }], types: { elements,
	// complexTypes, simpleTypes: [{ name, enumCount }] }, warnings }. References are local names (no prefix) and soapVersion
	// ('1.1', '1.2' or '') comes from the namespace of the SOAP binding extension. Every list holds at most 500 items (a warning
	// tells when one was cut), every text is plain data, at most 300 characters. Returns { ok: true, outline } or
	// { ok: false, error } (for text that is not well-formed XML, declares a DOCTYPE or is not a WSDL 1.1 definitions element).
	// Needs a DOM. Never throws.
	function buildWsdlOutline(text) {
		try {
			return buildWsdlOutlineCore(text);
		} catch (error) {
			return fail('The WSDL could not be summarized: ' + describeError(error));
		}
	}

	// ------------------------------------------------------------------
	// Public surface
	// ------------------------------------------------------------------

	window.SoapUiWsdl = Object.freeze({
		parse: parse,
		buildEnvelope: buildEnvelope,
		fallbackEnvelope: fallbackEnvelope,
		prettyPrintXml: prettyPrintXml,
		checkXml: checkXml,
		escapeXml: escapeXml,
		parseSoapFault: parseSoapFault,
		faultFromText: faultFromText,
		hasDoctype: hasDoctype,
		envelopeNamespace: envelopeNamespace,
		protocolHeaders: protocolHeaders,
		validateEnvelope: validateEnvelope,
		validateAction: validateAction,
		validateHeader: validateHeader,
		isSensitiveHeader: isSensitiveHeader,
		shellQuote: shellQuote,
		shortenSoapAction: shortenSoapAction,
		formatBytes: formatBytes,
		buildHashRoute: buildHashRoute,
		parseHashRoute: parseHashRoute,
		buildSvcutilCommand: buildSvcutilCommand,
		snippetKinds: SNIPPET_KINDS,
		buildSnippet: buildSnippet,
		buildAuthHeader: buildAuthHeader,
		wsPasswordDigest: wsPasswordDigest,
		utf8Bytes: utf8Bytes,
		base64Encode: base64Encode,
		buildWsSecurityHeader: buildWsSecurityHeader,
		applyWsSecurity: applyWsSecurity,
		sampleForClrType: sampleForClrType,
		buildFormTemplate: buildFormTemplate,
		inputKindForType: inputKindForType,
		validateXsdValue: validateXsdValue,
		sampleValueForType: sampleValueForType,
		tokenizeXml: tokenizeXml,
		findMatches: findMatches,
		buildWsdlOutline: buildWsdlOutline,
		curl: Object.freeze({ build: buildCurl })
	});
}());
