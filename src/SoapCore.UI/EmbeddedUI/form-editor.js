/*
 * form-editor.js
 *
 * Form view of the request envelope of one operation: the alternative to the raw XML view. The page shows it next
 * to the editor ("Form" and "XML" tabs). Page independent of the operation card: the card hands over a template (plain
 * data built from the WSDL), two functions to read and write the envelope text, and a container element.
 *
 * Rules for this file:
 *  - The XML text is the single source of truth. The form is rebuilt from it every time refresh() runs. An edit in the
 *    form is applied to a FRESH parse of the current text (never to a stale DOM), found by a path of
 *    { ns, name, index } steps from the Body down, serialised, pretty printed and handed to setText.
 *  - Markup is never assembled from strings. The DOM is built with createElement, createTextNode and textContent only,
 *    and every XML node is created with the DOM and serialised by XMLSerializer, so all values are escaped.
 *  - Template text and XML values reach the page only as text content, attribute values or control values.
 *  - Nothing is stored, sent or logged. Functions of this file never throw to the caller.
 *  - One delegated listener per event type is attached to the container and removed by dispose().
 *
 * Exposed on the frozen global SoapUiForm: mount(container, options) and isSupported().
 * Needs from the frozen global SoapUiWsdl: inputKindForType, validateXsdValue, sampleValueForType, prettyPrintXml and
 * envelopeNamespace (hasDoctype is used when present).
 */
(function () {
	'use strict';

	const NS_XSI = 'http://www.w3.org/2001/XMLSchema-instance';
	const NS_XMLNS = 'http://www.w3.org/2000/xmlns/';

	const MAX_ROWS = 2000;
	const MAX_TEXT_CHARS = 1000000;
	const MAX_TEMPLATE_NODES = 20000;
	const MAX_TEMPLATE_DEPTH = 30;
	const MAX_BUILD_ELEMENTS = 500;
	const MAX_BUILD_REPEAT = 25;
	const MAX_OTHER_LISTED = 200;
	const MAX_OTHER_TEXT = 160;
	const LONG_TEXT = 80;
	// Groups at this nesting level (the Body children are level 0) and deeper are collapsible.
	const COLLAPSIBLE_FROM = 2;

	const REQUIRED_FUNCTIONS = Object.freeze(['inputKindForType', 'validateXsdValue', 'sampleValueForType', 'prettyPrintXml', 'envelopeNamespace']);

	const MSG_BAD_XML = 'The request XML is not well-formed or is not a SOAP envelope. Fix it in the XML view.';
	const MSG_NO_MATCH = 'The request does not match the operation structure. Use the XML view.';
	const MSG_NO_MATCH_HINT = 'Use Reset sample in the XML view to restore the generated request.';
	const MSG_TOO_LARGE = 'The request is too large for the form. Use the XML view.';
	const MSG_BROKEN = 'The form could not be built. Use the XML view.';
	const MSG_XML_ONLY = 'Edit this part in the XML view.';
	const MSG_NOT_EXPANDED = 'Not expanded further. Edit this part in the XML view.';
	const MSG_ANY_CONTENT = 'This element accepts arbitrary XML content. Edit that part in the XML view.';
	const MSG_ROW_LIMIT = 'The form shows the first ' + MAX_ROWS + ' fields. Use the XML view for the rest.';
	const MSG_STALE = 'The request XML changed outside the form, so the form was reloaded. Repeat your change.';
	const MSG_FAILED = 'The change could not be applied to the request XML.';
	const MSG_CHOICE = 'More than one alternative of this choice is present. Only one is allowed.';
	const MSG_NIL = 'This element is nil (xsi:nil). Clear nil to edit its content.';
	const MSG_REQUIRED_ATTRIBUTE = 'This attribute is required.';
	const MSG_INVALID_VALUE = 'The value is not valid for this type.';

	const BLOCKED_TAGS = new Set(['script', 'style', 'iframe', 'object', 'embed', 'link', 'base', 'form', 'meta']);
	const PLAIN_ATTRIBUTES = new Set([
		'class', 'id', 'type', 'for', 'role', 'tabindex', 'hidden', 'title', 'placeholder', 'spellcheck',
		'autocomplete', 'name', 'value', 'rows', 'disabled', 'readonly', 'checked', 'open', 'inputmode'
	]);

	const DEFAULT_TYPE = Object.freeze({ xsd: 'string', enumValues: null, pattern: null });
	const DEFAULT_KIND = Object.freeze({ kind: 'text', inputMode: 'text', placeholder: '', pattern: null });
	const MONOSPACE_KINDS = new Set(['integer', 'number', 'datetime', 'date', 'time', 'duration', 'base64', 'guid']);

	let instanceCounter = 0;
	// The controller that owns a container, so a second mount into the same container takes it over cleanly.
	const mounted = new WeakMap();

	// ------------------------------------------------------------------
	// DOM helpers
	// ------------------------------------------------------------------

	function appendChild(node, child) {
		if (child === null || child === undefined || child === false) {
			return;
		}

		if (Array.isArray(child)) {
			for (const item of child) {
				appendChild(node, item);
			}

			return;
		}

		node.appendChild(child instanceof Node ? child : document.createTextNode(String(child)));
	}

	// Creates an element. Event handler attributes and inline presentation attributes are refused, every other attribute
	// must be on the allow list (or be an aria-* or data-* attribute). Children are nodes or plain text.
	function el(tag, attributes, children) {
		if (BLOCKED_TAGS.has(tag)) {
			throw new Error('Element not allowed: ' + tag);
		}

		const node = document.createElement(tag);
		if (attributes) {
			for (const key of Object.keys(attributes)) {
				const value = attributes[key];
				if (value === undefined || value === null || value === false) {
					continue;
				}

				const lower = key.toLowerCase();
				if (lower.startsWith('on') || lower === 'style') {
					throw new Error('Attribute not allowed: ' + key);
				}

				if (!PLAIN_ATTRIBUTES.has(lower) && !lower.startsWith('aria-') && !lower.startsWith('data-')) {
					throw new Error('Attribute not allowed: ' + key);
				}

				node.setAttribute(key, value === true ? '' : String(value));
			}
		}

		appendChild(node, children);
		return node;
	}

	function shorten(value, max) {
		const clean = String(value).replace(/\s+/g, ' ').trim();
		return clean.length > max ? clean.slice(0, max - 3) + '...' : clean;
	}

	// ------------------------------------------------------------------
	// SoapUiWsdl access (looked up on every call, wrapped, never trusted to succeed)
	// ------------------------------------------------------------------

	function wsdl() {
		return window.SoapUiWsdl;
	}

	function isSupported() {
		try {
			const api = wsdl();
			if (!api || typeof DOMParser !== 'function' || typeof XMLSerializer !== 'function') {
				return false;
			}

			return REQUIRED_FUNCTIONS.every((name) => typeof api[name] === 'function');
		} catch (error) {
			return false;
		}
	}

	function kindFor(type) {
		try {
			const result = wsdl().inputKindForType(type);
			if (result && typeof result.kind === 'string') {
				return result;
			}
		} catch (error) {
			return DEFAULT_KIND;
		}

		return DEFAULT_KIND;
	}

	// Returns { ok: true } or { ok: false, error }. A validator that fails never blocks the user.
	function checkValue(type, value) {
		try {
			const result = wsdl().validateXsdValue(type, value);
			if (result && result.ok === false) {
				return { ok: false, error: typeof result.error === 'string' && result.error !== '' ? result.error : MSG_INVALID_VALUE };
			}
		} catch (error) {
			return { ok: true };
		}

		return { ok: true };
	}

	function sampleFor(type) {
		try {
			const sample = wsdl().sampleValueForType(type);
			return typeof sample === 'string' ? sample : '';
		} catch (error) {
			return '';
		}
	}

	function hasDoctype(text) {
		try {
			if (typeof wsdl().hasDoctype === 'function') {
				return wsdl().hasDoctype(text) === true;
			}
		} catch (error) {
			return true;
		}

		return /<!DOCTYPE|<!ENTITY/i.test(text);
	}

	// ------------------------------------------------------------------
	// Template normalisation. The template is plain data from another module: every field is defaulted and checked here,
	// so the rest of the file can rely on the shape. The type objects are passed on untouched (the SoapUiWsdl functions
	// own their format).
	// ------------------------------------------------------------------

	function qkey(ns, name) {
		return '{' + ns + '}' + name;
	}

	function textOr(value, fallback) {
		return typeof value === 'string' ? value : fallback;
	}

	function countOr(value, fallback) {
		return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? Math.floor(value) : fallback;
	}

	function typeOr(type) {
		return type && typeof type === 'object' ? type : DEFAULT_TYPE;
	}

	function normalizeAttributes(list) {
		const result = [];
		if (!Array.isArray(list)) {
			return result;
		}

		for (const raw of list) {
			if (!raw || typeof raw !== 'object' || typeof raw.name !== 'string' || raw.name === '') {
				continue;
			}

			result.push({
				name: raw.name,
				ns: textOr(raw.ns, ''),
				type: typeOr(raw.type),
				required: raw.required === true,
				fixed: typeof raw.fixed === 'string' ? raw.fixed : null
			});
		}

		return result;
	}

	function normalizeList(list, depth, counter) {
		const result = [];
		if (!Array.isArray(list)) {
			return result;
		}

		for (const raw of list) {
			if (counter.nodes >= MAX_TEMPLATE_NODES) {
				break;
			}

			const node = normalizeNode(raw, depth, counter);
			if (node) {
				result.push(node);
			}
		}

		return result;
	}

	function normalizeNode(raw, depth, counter) {
		if (!raw || typeof raw !== 'object' || typeof raw.name !== 'string' || raw.name === '') {
			return null;
		}

		// An element that may occur zero times at most cannot be part of a request.
		const max = raw.max === null ? null : countOr(raw.max, 1);
		if (max === 0) {
			return null;
		}

		counter.nodes += 1;
		const tooDeep = depth >= MAX_TEMPLATE_DEPTH;
		const children = tooDeep ? [] : normalizeList(raw.children, depth + 1, counter);
		const ns = textOr(raw.ns, '');
		const choice = typeof raw.choiceGroup === 'string' && raw.choiceGroup !== '' ? raw.choiceGroup : null;
		return {
			name: raw.name,
			ns: ns,
			key: qkey(ns, raw.name),
			kind: raw.kind === 'complex' || children.length > 0 ? 'complex' : 'simple',
			type: typeOr(raw.type),
			children: children,
			attributes: normalizeAttributes(raw.attributes),
			min: countOr(raw.min, 1),
			// Every alternative of a choice may be left out (another one is chosen), whatever its own minimum says.
			uiMin: choice !== null ? 0 : countOr(raw.min, 1),
			max: max,
			nillable: raw.nillable === true,
			choiceGroup: choice,
			anyContent: raw.anyContent === true,
			truncated: raw.truncated === true || (tooDeep && Array.isArray(raw.children) && raw.children.length > 0),
			note: textOr(raw.note, '')
		};
	}

	function normalizeTemplate(raw) {
		const source = raw && typeof raw === 'object' ? raw : {};
		const counter = { nodes: 0 };
		return {
			operationName: textOr(source.operationName, ''),
			bodyParts: normalizeList(source.bodyParts, 0, counter),
			warnings: Array.isArray(source.warnings) ? source.warnings.filter((warning) => typeof warning === 'string' && warning !== '') : []
		};
	}

	function isRepeatable(node) {
		return node.max === null || node.max > 1;
	}

	// ------------------------------------------------------------------
	// XML helpers
	// ------------------------------------------------------------------

	function nsOf(node) {
		return node.namespaceURI || '';
	}

	function elementChildren(parent) {
		const result = [];
		for (let child = parent.firstElementChild; child; child = child.nextElementSibling) {
			result.push(child);
		}

		return result;
	}

	function isNil(element) {
		const value = element.getAttributeNS(NS_XSI, 'nil');
		return value === 'true' || value === '1';
	}

	// True when the element holds text that is not just indentation.
	function hasMeaningfulText(element) {
		for (let child = element.firstChild; child; child = child.nextSibling) {
			if ((child.nodeType === 3 || child.nodeType === 4) && child.data.trim() !== '') {
				return true;
			}
		}

		return false;
	}

	// The text of a simple element. Indentation around a line break counts as empty, like the pretty printer does.
	function readText(element) {
		const value = element.textContent;
		return value.trim() === '' && /[\r\n]/.test(value) ? '' : value;
	}

	// Replaces the text of an element and keeps its comments, processing instructions and attributes.
	function setElementText(element, value) {
		let target = null;
		let child = element.firstChild;
		while (child) {
			const next = child.nextSibling;
			if (child.nodeType === 3 || child.nodeType === 4) {
				if (target === null && value !== '') {
					target = child;
				} else {
					element.removeChild(child);
				}
			}

			child = next;
		}

		if (value === '') {
			return;
		}

		if (target === null) {
			element.appendChild(element.ownerDocument.createTextNode(value));
		} else if (target.nodeType === 4) {
			element.replaceChild(element.ownerDocument.createTextNode(value), target);
		} else {
			target.data = value;
		}
	}

	// Removes the element and text content, keeps comments.
	function clearContent(element) {
		let child = element.firstChild;
		while (child) {
			const next = child.nextSibling;
			if (child.nodeType === 1 || child.nodeType === 3 || child.nodeType === 4) {
				element.removeChild(child);
			}

			child = next;
		}
	}

	// Sets or removes xsi:nil. The prefix already bound to the namespace is reused, otherwise xsi is declared on the element.
	function setNil(element, on) {
		if (!on) {
			element.removeAttributeNS(NS_XSI, 'nil');
			return;
		}

		let prefix = element.lookupPrefix(NS_XSI);
		if (!prefix) {
			prefix = 'xsi';
			element.setAttributeNS(NS_XMLNS, 'xmlns:xsi', NS_XSI);
		}

		element.setAttributeNS(NS_XSI, prefix + ':nil', 'true');
	}

	function setAttributeValue(element, attr, value) {
		const remove = value === '' && !attr.required;
		if (attr.ns === '') {
			if (remove) {
				element.removeAttribute(attr.name);
			} else {
				element.setAttribute(attr.name, value);
			}

			return;
		}

		const existing = element.getAttributeNodeNS(attr.ns, attr.name);
		if (remove) {
			if (existing) {
				element.removeAttributeNode(existing);
			}
		} else if (existing) {
			existing.value = value;
		} else {
			const prefix = element.lookupPrefix(attr.ns);
			element.setAttributeNS(attr.ns, prefix ? prefix + ':' + attr.name : attr.name, value);
		}
	}

	// Returns { doc, body } for the text, or null when it is not a well-formed SOAP envelope of the given namespace with a Body.
	function parseEnvelope(text, envelopeNs) {
		if (typeof text !== 'string' || text.trim() === '' || !envelopeNs || hasDoctype(text) || !/^\uFEFF?\s*</.test(text)) {
			return null;
		}

		let doc;
		try {
			doc = new DOMParser().parseFromString(text, 'application/xml');
		} catch (error) {
			return null;
		}

		if (doc.getElementsByTagNameNS('*', 'parsererror').length > 0) {
			return null;
		}

		const root = doc.documentElement;
		if (!root || root.localName !== 'Envelope' || nsOf(root) !== envelopeNs) {
			return null;
		}

		for (let child = root.firstElementChild; child; child = child.nextElementSibling) {
			if (child.localName === 'Body' && nsOf(child) === envelopeNs) {
				return { doc: doc, body: child };
			}
		}

		return null;
	}

	function pathKey(path) {
		return JSON.stringify(path.map((step) => [step.ns, step.name, step.index]));
	}

	function nthChild(parent, step) {
		let seen = 0;
		for (let child = parent.firstElementChild; child; child = child.nextElementSibling) {
			if (child.localName === step.name && nsOf(child) === step.ns) {
				if (seen === step.index) {
					return child;
				}

				seen += 1;
			}
		}

		return null;
	}

	function resolvePath(body, path) {
		let current = body;
		for (const step of path) {
			current = nthChild(current, step);
			if (!current) {
				return null;
			}
		}

		return current;
	}

	// Maps the qualified name of every child element to the list of its occurrences, in document order.
	function groupChildren(parent) {
		const byKey = new Map();
		for (let child = parent.firstElementChild; child; child = child.nextElementSibling) {
			const key = qkey(nsOf(child), child.localName);
			const list = byKey.get(key);
			if (list) {
				list.push(child);
			} else {
				byKey.set(key, [child]);
			}
		}

		return byKey;
	}

	// The element a new occurrence goes before: the first sibling that the template orders after it, or null to append.
	function insertionPoint(parent, siblings, node) {
		const order = new Map();
		siblings.forEach((sibling, index) => {
			if (!order.has(sibling.key)) {
				order.set(sibling.key, index);
			}
		});

		const mine = order.get(node.key);
		for (let child = parent.firstElementChild; child; child = child.nextElementSibling) {
			const position = order.get(qkey(nsOf(child), child.localName));
			if (position !== undefined && mine !== undefined && position > mine) {
				return child;
			}
		}

		return null;
	}

	// The node after the last occurrence with the same name, or null when there is none.
	function afterLastOccurrence(parent, node) {
		let last = null;
		for (let child = parent.firstElementChild; child; child = child.nextElementSibling) {
			if (child.localName === node.name && nsOf(child) === node.ns) {
				last = child;
			}
		}

		return last ? last.nextSibling : undefined;
	}

	// ------------------------------------------------------------------
	// Building XML from the template (new optional elements and new list items)
	// ------------------------------------------------------------------

	// The namespace prefix already in scope is reused. When there is none the serialiser declares the namespace.
	function createTemplateElement(doc, parent, node) {
		if (node.ns === '') {
			return doc.createElementNS(null, node.name);
		}

		const prefix = parent.lookupPrefix(node.ns);
		return doc.createElementNS(node.ns, prefix ? prefix + ':' + node.name : node.name);
	}

	function setTemplateAttribute(element, attr, value) {
		if (attr.ns === '') {
			element.setAttribute(attr.name, value);
			return;
		}

		const prefix = element.lookupPrefix(attr.ns);
		element.setAttributeNS(attr.ns, prefix ? prefix + ':' + attr.name : attr.name, value);
	}

	// The content of a new element: sample text for a simple element, the required children (in template order, one
	// alternative per choice) for a complex one.
	function fillContent(doc, element, node, budget, depth) {
		if (node.kind === 'simple') {
			const sample = sampleFor(node.type);
			if (sample !== '') {
				element.appendChild(doc.createTextNode(sample));
			}

			return;
		}

		if (node.truncated) {
			return;
		}

		const chosen = new Set();
		for (const child of node.children) {
			if (child.min < 1) {
				continue;
			}

			if (child.choiceGroup !== null) {
				if (chosen.has(child.choiceGroup)) {
					continue;
				}

				chosen.add(child.choiceGroup);
			}

			const times = Math.min(child.min, MAX_BUILD_REPEAT);
			for (let index = 0; index < times; index += 1) {
				if (!buildElement(doc, element, null, child, budget, depth + 1)) {
					return;
				}
			}
		}
	}

	// Required and fixed attributes, then the content.
	function fillElement(doc, element, node, budget, depth) {
		for (const attr of node.attributes) {
			if (attr.fixed !== null) {
				setTemplateAttribute(element, attr, attr.fixed);
			} else if (attr.required) {
				setTemplateAttribute(element, attr, sampleFor(attr.type));
			}
		}

		fillContent(doc, element, node, budget, depth);
	}

	// Inserts a new element for the template node before the given node (null appends) and fills it. Returns null when the size limit is hit.
	function buildElement(doc, parent, before, node, budget, depth) {
		if (budget.count >= MAX_BUILD_ELEMENTS || depth > MAX_TEMPLATE_DEPTH) {
			return null;
		}

		budget.count += 1;
		const element = createTemplateElement(doc, parent, node);
		parent.insertBefore(element, before);
		fillElement(doc, element, node, budget, depth);
		return element;
	}

	// ------------------------------------------------------------------
	// Small widgets
	// ------------------------------------------------------------------

	function typeLabel(type) {
		const xsd = type && typeof type.xsd === 'string' ? type.xsd.trim() : '';
		return xsd.length > 40 ? xsd.slice(0, 37) + '...' : xsd;
	}

	function badge(label, className, title) {
		return el('span', { class: 'fe-badge' + (className ? ' ' + className : ''), title: title || null }, label);
	}

	function badgesFor(node) {
		const wrap = el('span', { class: 'fe-badges' });
		const type = node.kind === 'simple' ? typeLabel(node.type) : '';
		if (type !== '') {
			wrap.appendChild(badge(type, 'fe-badge-type', 'Type: ' + type));
		}

		if (node.uiMin === 0 && node.choiceGroup === null) {
			wrap.appendChild(badge('optional', 'fe-badge-optional', 'May be left out of the request.'));
		}

		if (isRepeatable(node)) {
			wrap.appendChild(badge('repeatable', 'fe-badge-repeatable', 'At least ' + node.min + ', at most ' + (node.max === null ? 'unbounded' : node.max) + ' occurrences.'));
		}

		if (node.nillable) {
			wrap.appendChild(badge('nillable', 'fe-badge-nillable', 'May be sent as nil (xsi:nil).'));
		}

		if (node.choiceGroup !== null) {
			wrap.appendChild(badge('choice', 'fe-badge-choice', 'One alternative of a choice. Only one alternative of the group may be present.'));
		}

		return wrap;
	}

	// A select with the given values. The current value is added (marked as not in the list) when it is none of them.
	function buildSelect(values, current, allowBlank, attributes) {
		const select = el('select', attributes);
		const seen = new Set();
		const add = (value, label) => {
			select.appendChild(el('option', { value: value }, label));
			seen.add(value);
		};

		if (allowBlank) {
			add('', '(not set)');
		}

		for (const value of values) {
			if (!seen.has(value)) {
				add(value, value === '' ? '(empty)' : value);
			}
		}

		if (!seen.has(current)) {
			add(current, current === '' ? '(empty)' : current + ' (not in list)');
		}

		select.value = current;
		return select;
	}

	// The control for a typed value. Text and numbers are always text inputs, so the lexical form the user types is kept.
	function buildControl(type, value, attributes, allowBlank) {
		const info = kindFor(type);
		const enumValues = type && Array.isArray(type.enumValues) ? type.enumValues.map(String) : [];
		if (info.kind === 'boolean') {
			const values = value === '1' || value === '0' ? ['true', 'false', value] : ['true', 'false'];
			return buildSelect(values, value, allowBlank, Object.assign({ class: 'fe-input' }, attributes));
		}

		if (info.kind === 'enum' && enumValues.length > 0) {
			return buildSelect(enumValues, value, allowBlank, Object.assign({ class: 'fe-input' }, attributes));
		}

		const mono = MONOSPACE_KINDS.has(info.kind) ? ' fe-mono' : '';
		if (info.kind === 'text' && (value.length > LONG_TEXT || /[\r\n]/.test(value))) {
			const area = el('textarea', Object.assign({ class: 'fe-input fe-textarea', rows: '3', spellcheck: 'false', autocomplete: 'off' }, attributes));
			area.value = value;
			return area;
		}

		const input = el('input', Object.assign({
			class: 'fe-input' + mono,
			type: 'text',
			autocomplete: 'off',
			spellcheck: 'false',
			inputmode: info.inputMode === 'numeric' || info.inputMode === 'decimal' ? info.inputMode : null,
			placeholder: typeof info.placeholder === 'string' && info.placeholder !== '' ? info.placeholder : null
		}, attributes));
		input.value = value;
		return input;
	}

	function firstEditableIn(node) {
		return node.querySelector('input:not([type="checkbox"]):not([disabled]), select:not([disabled]), textarea:not([disabled])')
			|| node.querySelector('input:not([disabled]), button:not([disabled])');
	}

	// ------------------------------------------------------------------
	// The controller
	// ------------------------------------------------------------------

	function createController(container, options) {
		instanceCounter += 1;
		const instance = instanceCounter;
		const readOnly = options.readOnly === true;
		const envelopeNs = wsdl().envelopeNamespace(options.version);
		const template = normalizeTemplate(options.template);
		const bodyKeys = new Set(template.bodyParts.map((node) => node.key));
		const openState = new Map();
		const invalid = new Set();
		let entries = new Map();
		let state = { rows: 0, truncated: false };
		let statusNode = null;
		let serial = 0;
		let disposed = false;
		let lastCount = -1;

		function uid(kind) {
			serial += 1;
			return 'fe' + instance + '-' + kind + serial;
		}

		function notify() {
			if (invalid.size === lastCount) {
				return;
			}

			lastCount = invalid.size;
			if (typeof options.onStateChange === 'function') {
				try {
					options.onStateChange({ invalidCount: invalid.size });
				} catch (error) {
					lastCount = -1;
				}
			}
		}

		function showStatus(message) {
			if (statusNode) {
				statusNode.textContent = message;
				statusNode.hidden = message === '';
			}
		}

		function reserveRow() {
			if (state.rows >= MAX_ROWS) {
				state.truncated = true;
				return false;
			}

			state.rows += 1;
			return true;
		}

		function register(entry) {
			entries.set(entry.key, entry);
			return entry;
		}

		// ----- validation -------------------------------------------------

		function validateEntry(entry, control) {
			if (entry.nil || entry.fixed) {
				return { ok: true };
			}

			const value = control.value;
			if (entry.type === 'attr') {
				if (value === '') {
					return entry.required ? { ok: false, error: MSG_REQUIRED_ATTRIBUTE } : { ok: true };
				}
			}

			return checkValue(entry.valueType, value);
		}

		function applyValidity(entry, control, result) {
			if (result.ok) {
				control.removeAttribute('aria-invalid');
				control.classList.remove('fe-invalid');
				entry.errorNode.textContent = '';
				entry.errorNode.hidden = true;
				invalid.delete(entry.key);
			} else {
				control.setAttribute('aria-invalid', 'true');
				control.classList.add('fe-invalid');
				entry.errorNode.textContent = result.error;
				entry.errorNode.hidden = false;
				invalid.add(entry.key);
			}
		}

		// ----- editing the XML ---------------------------------------------

		// Applies a change to a fresh parse of the current text and hands the result to setText.
		// The mutator gets (doc, body) and returns false when the path it was given no longer exists.
		function edit(mutator) {
			let text;
			try {
				text = options.getText();
			} catch (error) {
				return { ok: false, message: MSG_FAILED };
			}

			const parsed = parseEnvelope(text, envelopeNs);
			if (!parsed) {
				return { ok: false, message: MSG_STALE };
			}

			let applied;
			try {
				applied = mutator(parsed.doc, parsed.body);
			} catch (error) {
				return { ok: false, message: MSG_FAILED };
			}

			if (applied === false) {
				return { ok: false, message: MSG_STALE };
			}

			let result;
			try {
				// Some browsers write the XML declaration when serialising, others do not. The declaration of the original text
				// is the one that is kept, whichever way the serialiser goes.
				const declaration = /^\uFEFF?\s*(<\?xml\s[^>]*?\?>)/.exec(text);
				const serialized = new XMLSerializer().serializeToString(parsed.doc).replace(/^\uFEFF?\s*<\?xml\s[^>]*?\?>\s*/, '');
				const raw = (declaration ? declaration[1] : '') + serialized;
				try {
					result = wsdl().prettyPrintXml(raw);
				} catch (error) {
					result = raw;
				}

				options.setText(result);
			} catch (error) {
				return { ok: false, message: MSG_FAILED };
			}

			return { ok: true, message: '' };
		}

		// An edit that changes the structure of the form: the form is rebuilt from the new text and the focus goes back.
		function structural(mutator, focus) {
			const outcome = edit(mutator);
			render();
			if (outcome.ok) {
				restoreFocus(focus);
			} else {
				showStatus(outcome.message);
			}
		}

		function findByKey(key) {
			for (const node of container.querySelectorAll('[data-path]')) {
				if (node.getAttribute('data-path') === key) {
					return node;
				}
			}

			return null;
		}

		function restoreFocus(focus) {
			if (!focus) {
				return;
			}

			let target = findByKey(focus.key);
			if (target && focus.row) {
				target = firstEditableIn(target) || target;
			}

			// The row may lie beyond the row limit: the control the user came from is the next best place.
			if (!target && focus.fallback) {
				target = findByKey(focus.fallback);
			}

			if (!target) {
				return;
			}

			const details = target.closest('details');
			if (details && !details.open) {
				details.open = true;
			}

			target.focus();
		}

		function commitField(entry, control) {
			const result = validateEntry(entry, control);
			applyValidity(entry, control, result);
			notify();
			if (!result.ok || control.value === entry.committed) {
				return;
			}

			const value = control.value;
			const outcome = edit((doc, body) => {
				const element = resolvePath(body, entry.path);
				if (!element) {
					return false;
				}

				if (entry.type === 'attr') {
					setAttributeValue(element, entry.attr, value);
				} else {
					setElementText(element, value);
				}

				return true;
			});

			if (outcome.ok) {
				entry.committed = value;
				showStatus('');
			} else {
				render();
				showStatus(outcome.message);
			}
		}

		function toggleInclude(entry, include) {
			const siblingsPath = entry.parentPath;
			structural((doc, body) => {
				const parent = resolvePath(body, siblingsPath);
				if (!parent) {
					return false;
				}

				if (include) {
					buildElement(doc, parent, insertionPoint(parent, entry.siblings, entry.node), entry.node, { count: 0 }, siblingsPath.length);
					return true;
				}

				const element = resolvePath(body, entry.path);
				if (!element) {
					return false;
				}

				element.parentNode.removeChild(element);
				return true;
			}, { key: entry.key, row: false });
		}

		function toggleNil(entry, on) {
			structural((doc, body) => {
				const element = resolvePath(body, entry.path);
				if (!element) {
					return false;
				}

				if (on) {
					setNil(element, true);
					clearContent(element);
				} else {
					setNil(element, false);
					fillContent(doc, element, entry.node, { count: 0 }, entry.path.length);
				}

				return true;
			}, { key: entry.key, row: false });
		}

		function addItem(entry) {
			structural((doc, body) => {
				const parent = resolvePath(body, entry.parentPath);
				if (!parent) {
					return false;
				}

				const after = afterLastOccurrence(parent, entry.node);
				const before = after === undefined ? insertionPoint(parent, entry.siblings, entry.node) : after;
				buildElement(doc, parent, before, entry.node, { count: 0 }, entry.parentPath.length);
				return true;
			}, { key: 'r|' + pathKey(entry.parentPath.concat([{ ns: entry.node.ns, name: entry.node.name, index: entry.count }])), row: true, fallback: entry.key });
		}

		function removeItem(entry) {
			const last = entry.path[entry.path.length - 1];
			const remaining = entry.count - 1;
			const parentPath = entry.path.slice(0, -1);
			const fallback = '+|' + pathKey(parentPath) + '|' + entry.node.key;
			const focus = remaining === 0
				? { key: 'i|' + pathKey(parentPath.concat([{ ns: last.ns, name: last.name, index: 0 }])), row: false, fallback: fallback }
				: { key: 'r|' + pathKey(parentPath.concat([{ ns: last.ns, name: last.name, index: Math.min(last.index, remaining - 1) }])), row: true, fallback: fallback };
			structural((doc, body) => {
				const element = resolvePath(body, entry.path);
				if (!element) {
					return false;
				}

				element.parentNode.removeChild(element);
				return true;
			}, focus);
		}

		// ----- building the form -------------------------------------------

		function labelText(node, path, count) {
			if (!isRepeatable(node) && count < 2) {
				return node.name;
			}

			return node.name + ' [' + (path[path.length - 1].index + 1) + ']';
		}

		function buildIncludeToggle(entry, checked) {
			const input = el('input', { type: 'checkbox', class: 'fe-checkbox', id: uid('i'), 'data-path': entry.key, 'aria-label': 'Include ' + entry.node.name });
			input.checked = checked;
			return el('label', { class: 'fe-check', for: input.id }, [input, el('span', null, 'Include')]);
		}

		function buildNilToggle(path, node, nil) {
			const entry = register({ type: 'nil', key: 'n|' + pathKey(path), path: path, node: node });
			const input = el('input', { type: 'checkbox', class: 'fe-checkbox', id: uid('z'), 'data-path': entry.key, 'aria-label': node.name + ' is nil', disabled: readOnly });
			input.checked = nil;
			return el('label', { class: 'fe-check fe-check-nil', for: input.id }, [input, el('span', null, 'nil')]);
		}

		// The Include checkbox of an optional element that is present and the Remove button of a list item.
		function buildActions(path, node, siblings, count) {
			if (readOnly) {
				return [];
			}

			const parentPath = path.slice(0, -1);
			const result = [];
			if (isRepeatable(node)) {
				const entry = register({ type: 'remove', key: 'x|' + pathKey(path), path: path, node: node, count: count });
				result.push(el('button', {
					type: 'button',
					class: 'fe-button',
					'data-path': entry.key,
					'aria-label': 'Remove ' + labelText(node, path, count),
					disabled: count <= node.uiMin
				}, 'Remove'));
			} else if (node.uiMin === 0) {
				const entry = register({ type: 'include', key: 'i|' + pathKey(path), path: path, parentPath: parentPath, node: node, siblings: siblings });
				result.push(buildIncludeToggle(entry, true));
			}

			return result;
		}

		function buildErrorNode(id) {
			return el('p', { class: 'fe-error', id: id, hidden: true });
		}

		function describedBy(errorId, noteId) {
			return noteId ? errorId + ' ' + noteId : errorId;
		}

		function buildAttributes(element, path, node) {
			if (node.attributes.length === 0) {
				return null;
			}

			const group = el('div', { class: 'fe-attrs', role: 'group', 'aria-label': 'Attributes of ' + node.name }, el('p', { class: 'fe-attrs-title' }, 'Attributes'));
			for (const attr of node.attributes) {
				if (!reserveRow()) {
					break;
				}

				const existing = element.getAttributeNodeNS(attr.ns === '' ? null : attr.ns, attr.name);
				const fixed = attr.fixed !== null;
				const value = existing ? existing.value : (fixed ? attr.fixed : '');
				const entry = register({
					type: 'attr',
					key: 'a|' + pathKey(path) + '|' + qkey(attr.ns, attr.name),
					path: path,
					attr: attr,
					valueType: attr.type,
					required: attr.required && !fixed,
					fixed: fixed,
					committed: value,
					errorNode: null,
					nil: false
				});
				const controlId = uid('a');
				const errorId = uid('e');
				const noteId = fixed ? uid('n') : null;
				const control = buildControl(attr.type, value, {
					id: controlId,
					'data-path': entry.key,
					'aria-describedby': describedBy(errorId, noteId),
					readonly: fixed,
					disabled: readOnly
				}, !attr.required);
				entry.errorNode = buildErrorNode(errorId);
				if (fixed && control.tagName === 'SELECT') {
					control.disabled = true;
				}

				const badges = el('span', { class: 'fe-badges' });
				const type = typeLabel(attr.type);
				if (type !== '') {
					badges.appendChild(badge(type, 'fe-badge-type', 'Type: ' + type));
				}

				badges.appendChild(attr.required ? badge('required', 'fe-badge-required', 'This attribute is required.') : badge('optional', 'fe-badge-optional', 'Leave empty to leave the attribute out.'));
				group.appendChild(el('div', { class: 'fe-row fe-attr-row' }, [
					el('div', { class: 'fe-head' }, [el('label', { class: 'fe-label', for: controlId }, attr.name), badges]),
					el('div', { class: 'fe-control' }, control),
					entry.errorNode,
					fixed ? el('p', { class: 'fe-note', id: noteId }, 'Fixed value: ' + attr.fixed) : null
				]));
				applyValidity(entry, control, validateEntry(entry, control));
			}

			return group;
		}

		function buildSimpleRow(element, path, node, siblings, count) {
			const key = pathKey(path);
			const nil = isNil(element);
			const value = nil ? '' : readText(element);
			const controlId = uid('c');
			const errorId = uid('e');
			const noteId = node.note === '' ? null : uid('n');
			const entry = register({
				type: 'value',
				key: 'v|' + key,
				path: path,
				valueType: node.type,
				committed: value,
				errorNode: buildErrorNode(errorId),
				nil: nil
			});
			const control = buildControl(node.type, value, {
				id: controlId,
				'data-path': entry.key,
				'aria-describedby': describedBy(errorId, noteId),
				disabled: readOnly || nil
			}, false);
			if (nil && control.tagName !== 'SELECT') {
				control.setAttribute('placeholder', 'nil');
			}

			const cell = [control];
			if (node.nillable || nil) {
				cell.push(buildNilToggle(path, node, nil));
			}

			const actions = buildActions(path, node, siblings, count);
			const row = el('div', { class: 'fe-row' + (nil ? ' fe-is-nil' : ''), 'data-path': 'r|' + key }, [
				el('div', { class: 'fe-head' }, [el('label', { class: 'fe-label', for: controlId }, labelText(node, path, count)), badgesFor(node)]),
				el('div', { class: 'fe-control' }, cell),
				actions.length > 0 ? el('div', { class: 'fe-actions' }, actions) : null,
				entry.errorNode,
				noteId ? el('p', { class: 'fe-note', id: noteId }, node.note) : null,
				buildAttributes(element, path, node)
			]);
			applyValidity(entry, control, validateEntry(entry, control));
			return row;
		}

		// A row that only names the element: its content cannot be edited here.
		function buildXmlOnlyRow(path, node, count, note) {
			return el('div', { class: 'fe-row fe-xml-only', 'data-path': 'r|' + pathKey(path) }, [
				el('div', { class: 'fe-head' }, [el('span', { class: 'fe-label' }, labelText(node, path, count)), badgesFor(node)]),
				el('p', { class: 'fe-note' }, note)
			]);
		}

		function buildAbsentRow(parentPath, node, siblings) {
			const path = parentPath.concat([{ ns: node.ns, name: node.name, index: 0 }]);
			const key = pathKey(path);
			let note = node.uiMin > 0 ? 'Required, but missing from the request XML.' : 'Not included in the request.';
			if (node.choiceGroup !== null) {
				note = 'Not included. Only one alternative of this choice may be present.';
			}

			if (readOnly) {
				return el('div', { class: 'fe-row fe-absent', 'data-path': 'r|' + key }, [
					el('div', { class: 'fe-head' }, [el('span', { class: 'fe-label' }, node.name), badgesFor(node)]),
					el('p', { class: 'fe-note' }, note)
				]);
			}

			const entry = register({ type: 'include', key: 'i|' + key, path: path, parentPath: parentPath, node: node, siblings: siblings });
			const input = el('input', { type: 'checkbox', class: 'fe-checkbox', id: uid('i'), 'data-path': entry.key });
			return el('div', { class: 'fe-row fe-absent', 'data-path': 'r|' + key }, [
				el('div', { class: 'fe-head' }, [
					el('label', { class: 'fe-check fe-include-label', for: input.id }, [input, el('span', { class: 'fe-label' }, 'Include ' + node.name)]),
					badgesFor(node)
				]),
				el('p', { class: 'fe-note' }, note)
			]);
		}

		function buildComplexGroup(element, path, node, siblings, count, depth) {
			const key = pathKey(path);
			const rowKey = 'r|' + key;
			const nil = isNil(element);
			const title = el('span', { class: 'fe-label' }, labelText(node, path, count));
			const badges = badgesFor(node);
			let wrapper;
			let body;
			if (depth >= COLLAPSIBLE_FROM) {
				const summaryId = uid('s');
				wrapper = el('details', { class: 'fe-group fe-deep', 'data-path': rowKey, open: openState.has(rowKey) ? openState.get(rowKey) : true });
				body = el('div', { class: 'fe-group-body', role: 'group', 'aria-labelledby': summaryId });
				wrapper.appendChild(el('summary', { class: 'fe-summary', id: summaryId }, [title, badges]));
				wrapper.appendChild(body);
			} else {
				wrapper = el('fieldset', { class: 'fe-group', 'data-path': rowKey });
				wrapper.appendChild(el('legend', { class: 'fe-legend' }, [title, badges]));
				body = el('div', { class: 'fe-group-body' });
				wrapper.appendChild(body);
			}

			const toolbar = buildActions(path, node, siblings, count);
			if (node.nillable || nil) {
				toolbar.push(buildNilToggle(path, node, nil));
			}

			if (toolbar.length > 0) {
				body.appendChild(el('div', { class: 'fe-toolbar' }, toolbar));
			}

			if (node.note !== '') {
				body.appendChild(el('p', { class: 'fe-note' }, node.note));
			}

			if (nil) {
				body.appendChild(el('p', { class: 'fe-note' }, MSG_NIL));
				appendIfPresent(body, buildAttributes(element, path, node));
			} else if (hasMeaningfulText(element)) {
				body.appendChild(el('p', { class: 'fe-note' }, 'This element has mixed content. ' + MSG_XML_ONLY));
			} else {
				appendIfPresent(body, buildAttributes(element, path, node));
				const before = state.rows;
				// The closing note goes after the fields and before the "Other content" group.
				renderNodes(element, path, node.children, depth + 1, body, () => {
					if (node.truncated) {
						body.appendChild(el('p', { class: 'fe-note' }, MSG_NOT_EXPANDED));
					} else if (node.anyContent) {
						body.appendChild(el('p', { class: 'fe-note' }, MSG_ANY_CONTENT));
					} else if (node.children.length === 0 && node.attributes.length === 0 && before === state.rows) {
						body.appendChild(el('p', { class: 'fe-note' }, 'This element has no fields.'));
					}
				});
			}

			return wrapper;
		}

		function appendIfPresent(parent, child) {
			if (child) {
				parent.appendChild(child);
			}
		}

		function buildOccurrence(element, path, node, siblings, count, depth) {
			const children = elementChildren(element);
			if (node.kind === 'simple') {
				if (children.length > 0 || node.anyContent) {
					return buildXmlOnlyRow(path, node, count, MSG_XML_ONLY);
				}

				return buildSimpleRow(element, path, node, siblings, count);
			}

			if (node.anyContent && node.children.length === 0 && node.attributes.length === 0) {
				return buildXmlOnlyRow(path, node, count, MSG_XML_ONLY);
			}

			return buildComplexGroup(element, path, node, siblings, count, depth);
		}

		function buildAddButton(parentPath, node, siblings, count) {
			const entry = register({ type: 'add', key: '+|' + pathKey(parentPath) + '|' + node.key, parentPath: parentPath, node: node, siblings: siblings, count: count });
			const full = node.max !== null && count >= node.max;
			return el('div', { class: 'fe-add' }, el('button', {
				type: 'button',
				class: 'fe-button',
				'data-path': entry.key,
				'aria-label': 'Add item to ' + node.name,
				disabled: full
			}, 'Add item'));
		}

		function renderOccurrences(parentEl, parentPath, node, siblings, found, depth, out) {
			if (found.length === 0) {
				if (reserveRow()) {
					out.appendChild(buildAbsentRow(parentPath, node, siblings));
				}

				return;
			}

			const repeatable = isRepeatable(node);
			const holder = repeatable ? el('div', { class: 'fe-list', role: 'group', 'aria-label': node.name + ' items' }) : out;
			for (let index = 0; index < found.length; index += 1) {
				if (!reserveRow()) {
					break;
				}

				const path = parentPath.concat([{ ns: node.ns, name: node.name, index: index }]);
				holder.appendChild(buildOccurrence(found[index], path, node, siblings, found.length, depth));
			}

			if (repeatable) {
				if (!readOnly) {
					holder.appendChild(buildAddButton(parentPath, node, siblings, found.length));
				}

				out.appendChild(holder);
			}
		}

		function buildOthers(parentPath, items) {
			const key = 'o|' + pathKey(parentPath);
			const list = el('ul', { class: 'fe-other-list' });
			for (const item of items.slice(0, MAX_OTHER_LISTED)) {
				const text = elementChildren(item).length > 0 ? '(contains child elements)' : shorten(item.textContent, MAX_OTHER_TEXT);
				list.appendChild(el('li', { class: 'fe-other-item' }, [
					el('span', { class: 'fe-mono fe-other-name', title: nsOf(item) === '' ? 'No namespace' : nsOf(item) }, item.localName),
					el('span', { class: 'fe-other-text' }, text === '' ? '(empty)' : text)
				]));
			}

			return el('details', { class: 'fe-group fe-other', 'data-path': key, open: openState.get(key) === true }, [
				el('summary', { class: 'fe-summary' }, 'Other content (' + items.length + ')'),
				el('div', { class: 'fe-group-body' }, [
					el('p', { class: 'fe-note' }, 'These elements are not part of the operation structure. They are kept as they are. Edit them in the XML view.'),
					list
				])
			]);
		}

		function renderNodes(parentEl, parentPath, nodes, depth, out, closing) {
			const byKey = groupChildren(parentEl);
			const known = new Set(nodes.map((node) => node.key));
			const choices = new Map();
			for (const node of nodes) {
				if (state.rows >= MAX_ROWS) {
					state.truncated = true;
					break;
				}

				const found = byKey.get(node.key) || [];
				if (node.choiceGroup !== null && found.length > 0) {
					choices.set(node.choiceGroup, (choices.get(node.choiceGroup) || 0) + 1);
				}

				renderOccurrences(parentEl, parentPath, node, nodes, found, depth, out);
			}

			for (const count of choices.values()) {
				if (count > 1) {
					out.appendChild(el('p', { class: 'fe-note fe-warning', role: 'note' }, MSG_CHOICE));
					break;
				}
			}

			if (closing) {
				closing();
			}

			if (state.truncated) {
				return;
			}

			const others = [];
			for (let child = parentEl.firstElementChild; child; child = child.nextElementSibling) {
				if (!known.has(qkey(nsOf(child), child.localName))) {
					others.push(child);
				}
			}

			if (others.length > 0) {
				out.appendChild(buildOthers(parentPath, others));
			}
		}

		function buildMessage(message, hint) {
			return el('div', { class: 'fe-message', role: 'status', tabindex: '-1' }, [
				el('p', { class: 'fe-message-text' }, message),
				hint ? el('p', { class: 'fe-note' }, hint) : null
			]);
		}

		function buildContent() {
			let text = '';
			try {
				text = options.getText();
			} catch (error) {
				text = '';
			}

			if (typeof text === 'string' && text.length > MAX_TEXT_CHARS) {
				return buildMessage(MSG_TOO_LARGE, null);
			}

			const parsed = parseEnvelope(text, envelopeNs);
			if (!parsed) {
				return buildMessage(MSG_BAD_XML, null);
			}

			let matched = false;
			for (let child = parsed.body.firstElementChild; child; child = child.nextElementSibling) {
				if (bodyKeys.has(qkey(nsOf(child), child.localName))) {
					matched = true;
					break;
				}
			}

			if (!matched) {
				return buildMessage(MSG_NO_MATCH, MSG_NO_MATCH_HINT);
			}

			const form = el('div', { class: 'fe-form' });
			for (const warning of template.warnings) {
				form.appendChild(el('p', { class: 'fe-note fe-template-warning' }, warning));
			}

			renderNodes(parsed.body, [], template.bodyParts, 0, form);
			if (state.truncated) {
				form.appendChild(el('p', { class: 'fe-note fe-limit', role: 'note' }, MSG_ROW_LIMIT));
			}

			return form;
		}

		function render() {
			if (disposed) {
				return;
			}

			entries = new Map();
			invalid.clear();
			state = { rows: 0, truncated: false };
			statusNode = el('p', { class: 'fe-status', role: 'status', 'aria-live': 'polite', hidden: true });
			let content;
			try {
				content = buildContent();
			} catch (error) {
				entries = new Map();
				invalid.clear();
				content = buildMessage(MSG_BROKEN, null);
			}

			container.textContent = '';
			container.appendChild(statusNode);
			container.appendChild(content);
			notify();
		}

		// ----- events (one delegated listener per type) ------------------------

		function entryOf(event) {
			const target = event.target;
			if (!(target instanceof Element)) {
				return null;
			}

			const holder = target.closest('[data-path]');
			if (!holder || !container.contains(holder)) {
				return null;
			}

			const entry = entries.get(holder.getAttribute('data-path'));
			return entry ? { entry: entry, control: holder } : null;
		}

		function onInput(event) {
			const found = entryOf(event);
			if (!found || (found.entry.type !== 'value' && found.entry.type !== 'attr')) {
				return;
			}

			applyValidity(found.entry, found.control, validateEntry(found.entry, found.control));
			notify();
		}

		function onChange(event) {
			const found = entryOf(event);
			if (!found || readOnly) {
				return;
			}

			const entry = found.entry;
			if (entry.type === 'value' || entry.type === 'attr') {
				commitField(entry, found.control);
			} else if (entry.type === 'include') {
				toggleInclude(entry, found.control.checked);
			} else if (entry.type === 'nil') {
				toggleNil(entry, found.control.checked);
			}
		}

		function onClick(event) {
			if (readOnly || !(event.target instanceof Element)) {
				return;
			}

			const button = event.target.closest('button');
			if (!button || button.disabled || !container.contains(button)) {
				return;
			}

			const entry = entries.get(button.getAttribute('data-path'));
			if (!entry) {
				return;
			}

			if (entry.type === 'add') {
				addItem(entry);
			} else if (entry.type === 'remove') {
				removeItem(entry);
			}
		}

		// Enter in a text input commits the value. So does Ctrl+Enter or Cmd+Enter in a text area, and it is not stopped, so
		// the shortcut that sends the request still works and sends what was just typed.
		function onKeydown(event) {
			if (event.key !== 'Enter' || event.isComposing || readOnly) {
				return;
			}

			const target = event.target;
			const isInput = target instanceof HTMLInputElement && target.type === 'text';
			const isArea = target instanceof HTMLTextAreaElement && (event.ctrlKey || event.metaKey);
			if (!isInput && !isArea) {
				return;
			}

			const found = entryOf(event);
			if (found && (found.entry.type === 'value' || found.entry.type === 'attr')) {
				commitField(found.entry, found.control);
			}
		}

		function onToggle(event) {
			const target = event.target;
			if (target instanceof HTMLDetailsElement && target.hasAttribute('data-path')) {
				openState.set(target.getAttribute('data-path'), target.open);
			}
		}

		container.addEventListener('input', onInput);
		container.addEventListener('change', onChange);
		container.addEventListener('click', onClick);
		container.addEventListener('keydown', onKeydown);
		container.addEventListener('toggle', onToggle, true);
		container.classList.add('fe-root');

		function dispose() {
			if (disposed) {
				return;
			}

			disposed = true;
			container.removeEventListener('input', onInput);
			container.removeEventListener('change', onChange);
			container.removeEventListener('click', onClick);
			container.removeEventListener('keydown', onKeydown);
			container.removeEventListener('toggle', onToggle, true);
			container.classList.remove('fe-root');
			container.textContent = '';
			entries.clear();
			invalid.clear();
			openState.clear();
		}

		function focusFirst() {
			if (disposed) {
				return;
			}

			const target = firstEditableIn(container) || container.querySelector('input:not([disabled]), button:not([disabled]), summary') || container.querySelector('.fe-message');
			if (target) {
				target.focus();
			}
		}

		return Object.freeze({
			refresh: render,
			dispose: dispose,
			invalidCount: () => invalid.size,
			focusFirst: focusFirst
		});
	}

	// A controller that does nothing, for when the form could not be built. The container shows why.
	function brokenController(container, message) {
		try {
			if (container instanceof Element) {
				container.textContent = '';
				container.appendChild(el('div', { class: 'fe-message', role: 'status', tabindex: '-1' }, el('p', { class: 'fe-message-text' }, message)));
			}
		} catch (error) {
			// There is nowhere left to report to.
		}

		return Object.freeze({
			refresh: () => undefined,
			dispose: () => {
				try {
					if (container instanceof Element) {
						container.textContent = '';
					}
				} catch (error) {
					// Nothing to clean up.
				}
			},
			invalidCount: () => 0,
			focusFirst: () => undefined
		});
	}

	// Builds the form in the container and returns its controller. Never throws: when the form cannot be built the
	// container shows a message and the controller does nothing.
	function mount(container, options) {
		try {
			if (!(container instanceof Element)) {
				return brokenController(null, MSG_BROKEN);
			}

			const settings = options && typeof options === 'object' ? options : {};
			if (!isSupported() || typeof settings.getText !== 'function' || typeof settings.setText !== 'function') {
				return brokenController(container, MSG_BROKEN);
			}

			const previous = mounted.get(container);
			if (previous) {
				previous.dispose();
			}

			const controller = createController(container, settings);
			mounted.set(container, controller);
			controller.refresh();
			return controller;
		} catch (error) {
			return brokenController(container, MSG_BROKEN);
		}
	}

	window.SoapUiForm = Object.freeze({
		mount: mount,
		isSupported: isSupported
	});
}());
