/*
 * xml-view.js
 *
 * The XML views of the SoapCore.UI explorer. This file knows nothing about services or operations.
 *
 *  - attachEditor(textarea) colours the text of an editor. The textarea stays the real editor, a read-only layer behind it
 *    shows the colours.
 *  - createViewer(options) makes a viewer for XML text (the response) with colours, folding of elements and search.
 *
 * The tokens come from SoapUiWsdl.tokenizeXml and the matches from SoapUiWsdl.findMatches (wsdl-parser.js). When one of them is
 * missing or fails, or the text is too large, the editor stays a plain textarea and the viewer shows plain text.
 *
 * Rules for this file: the DOM is built with createElement, createTextNode and textContent only, so text from a server is
 * never read as markup. The colours are CSS classes in style.css.
 */
(function () {
	'use strict';

	const MAX_COLOR_CHARS = 256 * 1024;
	const PAINT_DELAY_MS = 120;
	const SEARCH_DELAY_MS = 120;
	const CHUNK_LINES = 500;
	const SCROLL_MARGIN = 24;
	const TOKEN_TYPES = new Set(['decl', 'comment', 'cdata', 'doctype', 'punct', 'tag', 'attr', 'string', 'text']);

	// ------------------------------------------------------------------
	// Helpers
	// ------------------------------------------------------------------

	// Creates an element. Event handler and inline style attributes are refused. Children are nodes or plain text.
	function el(tag, attributes, children) {
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

				node.setAttribute(key, value === true ? '' : String(value));
			}
		}

		for (const child of [].concat(children === undefined ? [] : children)) {
			if (child !== null && child !== undefined && child !== false) {
				node.appendChild(child instanceof Node ? child : document.createTextNode(String(child)));
			}
		}

		return node;
	}

	function hasParserFunction(name) {
		return !!window.SoapUiWsdl && typeof window.SoapUiWsdl[name] === 'function';
	}

	function coversText(tokens, text) {
		let length = 0;
		for (const token of tokens) {
			length += token.text.length;
		}

		return length === text.length;
	}

	// The tokens of a text, or null when it cannot be coloured. The tokens must add up to the text, or the colours would not line up.
	function tokenize(text) {
		if (!hasParserFunction('tokenizeXml') || text.length > MAX_COLOR_CHARS) {
			return null;
		}

		try {
			const result = window.SoapUiWsdl.tokenizeXml(text, MAX_COLOR_CHARS);
			const tokens = result && result.ok === true && Array.isArray(result.tokens) ? result.tokens : null;
			return tokens !== null && coversText(tokens, text) ? tokens : null;
		} catch (error) {
			return null;
		}
	}

	function tokenClass(type) {
		return 'xv-' + (TOKEN_TYPES.has(type) ? type : 'text');
	}

	// Finds the matches of a query. Returns { ranges, truncated } or null when the search is not available.
	function findRanges(text, query, caseSensitive) {
		if (!hasParserFunction('findMatches')) {
			return null;
		}

		try {
			const result = window.SoapUiWsdl.findMatches(text, query, { caseSensitive: caseSensitive });
			return result && result.ok === true && Array.isArray(result.ranges) ? result : null;
		} catch (error) {
			return null;
		}
	}

	// Appends the text of value, which starts at offset in the whole text, with a mark around each part that a range covers.
	function appendWithMarks(parent, value, offset, ranges, current) {
		let at = 0;
		for (const range of ranges) {
			const from = Math.max(range.start - offset, at);
			const to = Math.min(range.end - offset, value.length);
			if (to <= from) {
				continue;
			}

			if (from > at) {
				parent.appendChild(document.createTextNode(value.slice(at, from)));
			}

			const classes = 'xv-match' + (range.index === current ? ' xv-match-current' : '');
			parent.appendChild(el('mark', { class: classes, 'data-m': range.index }, value.slice(from, to)));
			at = to;
		}

		if (at < value.length) {
			parent.appendChild(document.createTextNode(value.slice(at)));
		}
	}

	// ------------------------------------------------------------------
	// Coloured editor
	// ------------------------------------------------------------------

	function tokenFragment(tokens) {
		const fragment = document.createDocumentFragment();
		for (const token of tokens) {
			fragment.appendChild(token.type === 'text' ? document.createTextNode(token.text) : el('span', { class: tokenClass(token.type) }, token.text));
		}

		return fragment;
	}

	// Wraps the textarea in a container with a layer behind it. The textarea text is transparent while the layer is on, the caret and
	// the selection stay visible. Returns { root, refresh, dispose }: root is the container (it takes the place of the textarea
	// when that is in a page already), refresh colours the current text at once, for text that was set by code.
	function attachEditor(textarea) {
		const layer = el('pre', { class: 'xv-layer', 'aria-hidden': 'true' });
		const root = el('div', { class: 'xv-editor' });
		if (textarea.parentNode) {
			textarea.parentNode.insertBefore(root, textarea);
		}

		root.appendChild(layer);
		root.appendChild(textarea);
		textarea.classList.add('xv-input');
		let timer = 0;

		const syncScroll = () => {
			layer.scrollTop = textarea.scrollTop;
			layer.scrollLeft = textarea.scrollLeft;
		};
		const paint = () => {
			window.clearTimeout(timer);
			const value = textarea.value;
			const tokens = tokenize(value);
			if (tokens === null) {
				root.classList.remove('xv-on');
				layer.replaceChildren();
				return;
			}

			layer.replaceChildren(tokenFragment(tokens), document.createTextNode('\n'));
			root.classList.add('xv-on');
			syncScroll();
		};
		const onInput = () => {
			// Until the colours are painted the layer shows the new text without them, so the text never shows up late.
			if (root.classList.contains('xv-on')) {
				layer.textContent = textarea.value + '\n';
			}

			window.clearTimeout(timer);
			timer = window.setTimeout(paint, PAINT_DELAY_MS);
		};

		textarea.addEventListener('input', onInput);
		textarea.addEventListener('scroll', syncScroll);
		paint();
		return {
			root: root,
			refresh: paint,
			dispose: () => {
				window.clearTimeout(timer);
				textarea.removeEventListener('input', onInput);
				textarea.removeEventListener('scroll', syncScroll);
			}
		};
	}

	// ------------------------------------------------------------------
	// Viewer model: lines, segments and folds
	// ------------------------------------------------------------------

	// Maps character offsets of a text to line numbers (0 based) and tells whether the rest of a line is blank.
	function indexLines(text) {
		const starts = [0];
		for (let at = text.indexOf('\n'); at !== -1; at = text.indexOf('\n', at + 1)) {
			starts.push(at + 1);
		}

		const lineAt = (offset) => {
			let low = 0;
			let high = starts.length - 1;
			while (low < high) {
				const middle = (low + high + 1) >> 1;
				if (starts[middle] <= offset) {
					low = middle;
				} else {
					high = middle - 1;
				}
			}

			return low;
		};
		return {
			lineAt: lineAt,
			blankBefore: (offset) => text.slice(starts[lineAt(offset)], offset).trim() === '',
			blankAfter: (offset) => {
				const line = lineAt(offset);
				return text.slice(offset, line + 1 < starts.length ? starts[line + 1] - 1 : text.length).trim() === '';
			}
		};
	}

	function pushSegment(segments, type, text, start) {
		if (text !== '') {
			segments.push({ type: type, text: text, start: start });
		}
	}

	// One list of segments { type, text, start } per line. A token with line breaks in it is cut at them.
	function splitIntoLines(tokens) {
		const lines = [[]];
		let offset = 0;
		for (const token of tokens) {
			let from = 0;
			for (;;) {
				const at = token.text.indexOf('\n', from);
				const piece = token.text.slice(from, at === -1 ? token.text.length : at);
				pushSegment(lines[lines.length - 1], token.type, at !== -1 && piece.endsWith('\r') ? piece.slice(0, -1) : piece, offset + from);
				if (at === -1) {
					break;
				}

				lines.push([]);
				from = at + 1;
			}

			offset += token.text.length;
		}

		return lines;
	}

	// A fold needs the start tag to end its line and the end tag to stand on a line of its own, so hiding lines hides nothing else.
	function addFold(element, closeStart, closeEnd, lines, folds) {
		const start = lines.lineAt(element.end - 1);
		const end = lines.lineAt(closeStart);
		if (end > start && !folds.has(start) && lines.blankAfter(element.end) && lines.blankBefore(closeStart) && lines.blankAfter(closeEnd)) {
			folds.set(start, { start: start, end: end, name: element.name, depth: element.depth });
		}
	}

	// Handles a tag that ends at the offset end. A start tag opens an element, an end tag closes the nearest open element of that name.
	function endTag(text, tag, end, stack, lines, folds) {
		if (!tag.closing) {
			if (text.charAt(end - 2) !== '/') {
				stack.push({ name: tag.name, end: end, depth: stack.length });
			}

			return;
		}

		let at = stack.length - 1;
		while (at >= 0 && stack[at].name !== tag.name) {
			at -= 1;
		}

		if (at >= 0) {
			const element = stack[at];
			stack.length = at;
			addFold(element, tag.start, end, lines, folds);
		}
	}

	// The elements that can be folded, as a Map from the line of the start tag to { start, end, name, depth }.
	function findFolds(text, tokens, lines) {
		const folds = new Map();
		const stack = [];
		let tag = null;
		let offset = 0;
		for (const token of tokens) {
			const start = offset;
			offset += token.text.length;
			if (token.type === 'tag' && tag !== null && tag.name === '') {
				tag.name = token.text;
			} else if (token.type === 'punct' && token.text.charAt(0) === '<') {
				tag = { start: start, closing: text.charAt(start + 1) === '/', name: '' };
			} else if (token.type === 'punct' && tag !== null && token.text.endsWith('>')) {
				endTag(text, tag, offset, stack, lines, folds);
				tag = null;
			}
		}

		return folds;
	}

	// The lines, segments and folds of a text, or null when the text cannot be coloured.
	function buildModel(text) {
		const tokens = tokenize(text);
		if (tokens === null) {
			return null;
		}

		const lines = indexLines(text);
		const folds = findFolds(text, tokens, lines);
		return {
			lines: lines,
			segments: splitIntoLines(tokens),
			folds: folds,
			foldList: Array.from(folds.values()).sort((a, b) => a.start - b.start)
		};
	}

	// Which lines a set of collapsed folds hides: the lines after the start line up to the end tag.
	function computeHidden(model, collapsed) {
		const hidden = new Array(model.segments.length).fill(false);
		let until = -1;
		for (let line = 0; line < hidden.length; line += 1) {
			if (line <= until) {
				hidden[line] = true;
			} else if (collapsed.has(line)) {
				until = model.folds.get(line).end;
			}
		}

		return hidden;
	}

	// ------------------------------------------------------------------
	// Viewer DOM
	// ------------------------------------------------------------------

	function button(label, text) {
		return el('button', { class: 'btn btn-small', type: 'button', 'aria-label': label === text ? false : label }, text);
	}

	function buildSearchGroup(viewer) {
		viewer.input = el('input', {
			class: 'input input-small xv-query',
			type: 'search',
			autocomplete: 'off',
			spellcheck: 'false',
			placeholder: 'Search',
			title: 'Enter: next match. Shift+Enter: previous match. Esc: clear.',
			'aria-label': 'Search in the ' + viewer.label
		});
		viewer.caseBox = el('input', { type: 'checkbox' });
		viewer.prev = button('Previous match', 'Prev');
		viewer.next = button('Next match', 'Next');
		viewer.status = el('span', { class: 'xv-status muted', role: 'status', 'aria-live': 'polite' });
		viewer.searchGroup = el('span', { class: 'xv-group' }, [
			viewer.input,
			viewer.prev,
			viewer.next,
			el('label', { class: 'check xv-check' }, [viewer.caseBox, 'Match case']),
			viewer.status
		]);
	}

	function buildFoldGroup(viewer) {
		viewer.expand = button('Expand all', 'Expand all');
		viewer.collapse = button('Collapse all', 'Collapse all');
		viewer.foldGroup = el('span', { class: 'xv-group' }, [viewer.expand, viewer.collapse]);
	}

	function buildViewerDom(viewer) {
		buildSearchGroup(viewer);
		buildFoldGroup(viewer);
		viewer.bar = el('div', { class: 'xv-bar', role: 'search', 'aria-label': 'Search and fold the ' + viewer.label, hidden: true }, [viewer.searchGroup, viewer.foldGroup]);
		viewer.more = el('button', { class: 'btn btn-small xv-more', type: 'button', hidden: true });
		viewer.content = el('div', { class: 'xv-content' });
		viewer.body = el('div', { class: 'xv-body', role: 'region', tabindex: '0', 'aria-label': viewer.label[0].toUpperCase() + viewer.label.slice(1) }, viewer.content);
		viewer.root = el('div', { class: 'xv-viewer', hidden: true }, [viewer.bar, viewer.body]);
	}

	function foldButton(viewer, fold) {
		const toggle = el('button', {
			class: 'xv-fold',
			type: 'button',
			'aria-expanded': String(!viewer.collapsed.has(fold.start)),
			'aria-label': 'Fold ' + fold.name
		});
		toggle.addEventListener('click', () => toggleFold(viewer, fold));
		return toggle;
	}

	// The coloured text of one line, with the marks of the search matches that fall on it.
	function fillCode(viewer, code, index) {
		const ranges = viewer.byLine.get(index) || [];
		const pieces = viewer.model.segments[index].map((segment) => {
			const piece = el('span', { class: tokenClass(segment.type) });
			appendWithMarks(piece, segment.text, segment.start, ranges, viewer.current);
			return piece;
		});
		code.replaceChildren(...pieces);
	}

	function buildLine(viewer, index) {
		const fold = viewer.model.folds.get(index);
		const toggle = fold ? foldButton(viewer, fold) : null;
		const marker = fold ? el('span', { class: 'xv-fold-end', 'aria-hidden': 'true', hidden: !viewer.collapsed.has(index) }, '\u2026 </' + fold.name + '>') : null;
		const code = el('span', { class: 'xv-code' });
		fillCode(viewer, code, index);
		const root = el('div', { class: 'xv-line', hidden: viewer.hidden[index] }, [el('span', { class: 'xv-gutter', 'data-n': index + 1 }, toggle), code, marker]);
		viewer.lines[index] = { root: root, code: code, toggle: toggle, marker: marker };
		return root;
	}

	function updateMore(viewer) {
		const left = viewer.model.segments.length - viewer.rendered;
		viewer.more.hidden = left <= 0;
		viewer.more.textContent = 'Show ' + Math.min(CHUNK_LINES, left) + ' more lines (' + left + ' not shown)';
	}

	// Renders the lines that are not shown yet, up to the line number upTo. The rest waits for the "Show more" button.
	function renderLines(viewer, upTo) {
		const end = Math.min(upTo, viewer.model.segments.length);
		const fragment = document.createDocumentFragment();
		for (let line = viewer.rendered; line < end; line += 1) {
			fragment.appendChild(buildLine(viewer, line));
		}

		viewer.rendered = Math.max(viewer.rendered, end);
		viewer.linesBox.appendChild(fragment);
		updateMore(viewer);
	}

	// Plain text, with the marks of the matches. Used when the text cannot be coloured.
	function fillPlain(viewer) {
		const pre = viewer.plainBox;
		pre.replaceChildren();
		appendWithMarks(pre, viewer.text, 0, viewer.matches, viewer.current);
	}

	function renderBody(viewer) {
		viewer.lines = [];
		viewer.rendered = 0;
		viewer.hidden = [];
		viewer.content.replaceChildren();
		if (viewer.model === null) {
			viewer.plainBox = el('pre', { class: 'xv-plain' });
			viewer.content.appendChild(viewer.plainBox);
			return;
		}

		viewer.linesBox = el('div', { class: 'xv-lines' });
		viewer.content.appendChild(viewer.linesBox);
		viewer.content.appendChild(viewer.more);
		renderLines(viewer, CHUNK_LINES);
	}

	// The bar is for text the viewer can work with: colours need tokens, search needs findMatches.
	function updateBar(viewer) {
		const folds = viewer.model !== null && viewer.model.foldList.length > 0;
		viewer.searchGroup.hidden = !viewer.searchable;
		viewer.foldGroup.hidden = !folds;
		viewer.bar.hidden = viewer.plain || (!viewer.searchable && !folds);
	}

	// ------------------------------------------------------------------
	// Folding
	// ------------------------------------------------------------------

	// Shows or hides the lines after the collapsed folds, and the state of the fold buttons that exist.
	function applyFolds(viewer) {
		viewer.hidden = computeHidden(viewer.model, viewer.collapsed);
		for (let line = 0; line < viewer.rendered; line += 1) {
			viewer.lines[line].root.hidden = viewer.hidden[line];
		}

		for (const fold of viewer.model.foldList) {
			const entry = viewer.lines[fold.start];
			if (entry) {
				const collapsed = viewer.collapsed.has(fold.start);
				entry.toggle.setAttribute('aria-expanded', String(!collapsed));
				entry.marker.hidden = !collapsed;
			}
		}
	}

	function toggleFold(viewer, fold) {
		if (viewer.collapsed.has(fold.start)) {
			viewer.collapsed.delete(fold.start);
		} else {
			viewer.collapsed.add(fold.start);
		}

		applyFolds(viewer);
	}

	// Collapse all folds every element below the root, so the first level stays visible. A document with only a root folds the root.
	function foldAll(viewer, collapse) {
		viewer.collapsed.clear();
		if (collapse) {
			const deep = viewer.model.foldList.filter((fold) => fold.depth > 0);
			for (const fold of deep.length > 0 ? deep : viewer.model.foldList) {
				viewer.collapsed.add(fold.start);
			}
		}

		applyFolds(viewer);
	}

	// Makes the line of a match visible: opens the folds around it and renders the lines up to it.
	function revealLine(viewer, line) {
		let opened = false;
		for (const fold of viewer.model.foldList) {
			if (fold.start < line && line <= fold.end && viewer.collapsed.delete(fold.start)) {
				opened = true;
			}
		}

		if (line >= viewer.rendered) {
			renderLines(viewer, Math.ceil((line + 1) / CHUNK_LINES) * CHUNK_LINES);
		}

		if (opened) {
			applyFolds(viewer);
		}
	}

	// ------------------------------------------------------------------
	// Search
	// ------------------------------------------------------------------

	function groupByLine(viewer) {
		const byLine = new Map();
		if (viewer.model === null) {
			return byLine;
		}

		for (const match of viewer.matches) {
			const line = viewer.model.lines.lineAt(match.start);
			byLine.set(line, (byLine.get(line) || []).concat(match));
		}

		return byLine;
	}

	function updateStatus(viewer) {
		const count = viewer.matches.length;
		let message = '';
		if (viewer.input.value !== '' && viewer.searchable) {
			message = count === 0 ? 'No matches' : (viewer.current + 1) + ' of ' + count + (viewer.truncated ? '+' : '');
		}

		viewer.status.textContent = message;
		viewer.prev.disabled = count === 0;
		viewer.next.disabled = count === 0;
	}

	function scrollToNode(viewer, node) {
		const box = viewer.body.getBoundingClientRect();
		const rect = node.getBoundingClientRect();
		if (rect.top < box.top + SCROLL_MARGIN) {
			viewer.body.scrollTop -= box.top + SCROLL_MARGIN - rect.top;
		} else if (rect.bottom > box.bottom - SCROLL_MARGIN) {
			viewer.body.scrollTop += rect.bottom - box.bottom + SCROLL_MARGIN;
		}
	}

	// Marks the current match and, when asked, scrolls it into view. A match inside a folded element opens the fold.
	function paintCurrent(viewer, scroll) {
		for (const node of Array.from(viewer.root.querySelectorAll('.xv-match-current'))) {
			node.classList.remove('xv-match-current');
		}

		if (viewer.current < 0) {
			return;
		}

		if (viewer.model !== null) {
			revealLine(viewer, viewer.model.lines.lineAt(viewer.matches[viewer.current].start));
		}

		const nodes = viewer.root.querySelectorAll('mark[data-m="' + viewer.current + '"]');
		for (const node of nodes) {
			node.classList.add('xv-match-current');
		}

		if (scroll && nodes.length > 0) {
			scrollToNode(viewer, nodes[0]);
		}
	}

	function repaintMatches(viewer, previousLines) {
		if (viewer.model === null) {
			fillPlain(viewer);
			return;
		}

		for (const line of new Set(previousLines.concat(Array.from(viewer.byLine.keys())))) {
			if (line < viewer.rendered) {
				fillCode(viewer, viewer.lines[line].code, line);
			}
		}
	}

	function runSearch(viewer, scroll) {
		window.clearTimeout(viewer.searchTimer);
		viewer.searchTimer = 0;
		const previousLines = Array.from(viewer.byLine.keys());
		const query = viewer.input.value;
		const found = query === '' || !viewer.searchable ? null : findRanges(viewer.text, query, viewer.caseBox.checked);
		viewer.matches = found === null ? [] : found.ranges.map((range, index) => ({ start: range.start, end: range.end, index: index }));
		viewer.truncated = found !== null && found.truncated === true;
		viewer.byLine = groupByLine(viewer);
		viewer.current = viewer.matches.length > 0 ? 0 : -1;
		repaintMatches(viewer, previousLines);
		paintCurrent(viewer, scroll);
		updateStatus(viewer);
	}

	function scheduleSearch(viewer) {
		window.clearTimeout(viewer.searchTimer);
		viewer.searchTimer = window.setTimeout(() => runSearch(viewer, true), SEARCH_DELAY_MS);
	}

	// Goes to the next (delta 1) or the previous (delta -1) match. A search that is still waiting for its delay runs first.
	function step(viewer, delta) {
		if (viewer.searchTimer !== 0) {
			runSearch(viewer, false);
		}

		const count = viewer.matches.length;
		if (count > 0) {
			viewer.current = (viewer.current + delta + count) % count;
			paintCurrent(viewer, true);
			updateStatus(viewer);
		}
	}

	function onQueryKey(viewer, event) {
		if (event.key === 'Enter') {
			event.preventDefault();
			step(viewer, event.shiftKey ? -1 : 1);
		} else if (event.key === 'Escape' && viewer.input.value !== '') {
			event.preventDefault();
			viewer.input.value = '';
			runSearch(viewer, false);
		}
	}

	// ------------------------------------------------------------------
	// Viewer
	// ------------------------------------------------------------------

	function setViewerText(viewer, value, options) {
		viewer.text = typeof value === 'string' ? value : '';
		viewer.plain = !!options && options.plain === true;
		viewer.searchable = !viewer.plain && hasParserFunction('findMatches');
		viewer.model = viewer.plain || viewer.text === '' ? null : buildModel(viewer.text);
		viewer.collapsed.clear();
		viewer.byLine = new Map();
		viewer.matches = [];
		viewer.current = -1;
		renderBody(viewer);
		updateBar(viewer);
		viewer.root.hidden = viewer.text === '';
		viewer.body.scrollTop = 0;
		viewer.body.scrollLeft = 0;
		runSearch(viewer, false);
	}

	function wireViewer(viewer) {
		viewer.input.addEventListener('input', () => scheduleSearch(viewer));
		viewer.input.addEventListener('keydown', (event) => onQueryKey(viewer, event));
		viewer.caseBox.addEventListener('change', () => runSearch(viewer, true));
		viewer.next.addEventListener('click', () => step(viewer, 1));
		viewer.prev.addEventListener('click', () => step(viewer, -1));
		viewer.expand.addEventListener('click', () => foldAll(viewer, false));
		viewer.collapse.addEventListener('click', () => foldAll(viewer, true));
		viewer.more.addEventListener('click', () => renderLines(viewer, viewer.rendered + CHUNK_LINES));
	}

	// A viewer for XML text. Returns { root, content, setText, dispose }. root goes into the page, content holds the text (select it
	// to let the user copy by hand), setText(text, { plain }) shows a text: with plain true it is shown as it is, without colours,
	// folding or search. A text that cannot be coloured is shown as plain text with search.
	function createViewer(options) {
		const viewer = {
			label: options && typeof options.label === 'string' && options.label !== '' ? options.label : 'xml text',
			text: '',
			plain: false,
			searchable: false,
			model: null,
			collapsed: new Set(),
			lines: [],
			hidden: [],
			rendered: 0,
			matches: [],
			byLine: new Map(),
			current: -1,
			truncated: false,
			searchTimer: 0
		};
		buildViewerDom(viewer);
		wireViewer(viewer);
		return {
			root: viewer.root,
			content: viewer.content,
			setText: (value, settings) => setViewerText(viewer, value, settings),
			dispose: () => window.clearTimeout(viewer.searchTimer)
		};
	}

	window.SoapUiXmlView = Object.freeze({
		attachEditor: attachEditor,
		createViewer: createViewer
	});
}());
