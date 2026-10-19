/*
 * app.js
 *
 * UI wiring of the SoapCore.UI explorer. Page independent logic (WSDL parsing, sample
 * envelopes, pretty printing, validation, curl) lives in wsdl-parser.js. The request Form view is
 * form-editor.js, the colours, folding and search of XML are xml-view.js. Both are optional: without
 * them the explorer works with plain text areas.
 *
 * Rules for this file:
 *  - The DOM is built with createElement, createTextNode and textContent only.
 *    Server data (names, paths, WSDL, responses) is never interpreted as markup.
 *  - Every request goes to the same origin. URLs from the API pass sameOriginUrl().
 *  - Nothing is stored except the theme preference. Credentials (Authorize), custom headers, envelopes and the request
 *    history live in memory only and are never written to the URL or to any storage.
 *  - There is no logging, so request and response data cannot leak that way.
 *  - Maps keyed by service ids or operation names are Maps, never plain objects.
 */
(function () {
	'use strict';

	const Wsdl = window.SoapUiWsdl;

	const MAX_BODY_BYTES = 5 * 1024 * 1024;
	const MAX_PRETTY_CHARS = 1000000;
	const DEFAULT_TIMEOUT_SECONDS = 60;
	const MIN_TIMEOUT_SECONDS = 1;
	const STATUS_FADE_MS = 6000;
	const LIVE_CHECK_DELAY_MS = 400;
	const MAX_LIVE_CHECK_CHARS = 256 * 1024;
	const REVOKE_DELAY_MS = 1000;
	const HISTORY_LIMIT = 20;
	const HISTORY_MAX_TEXT = 64 * 1024;
	const SMALL_SCREEN_QUERY = '(max-width: 800px)';
	const THEME_KEY = 'soapcore-ui-theme';
	const THEMES = ['system', 'light', 'dark'];
	const DEFAULT_API_KEY_HEADER = 'X-Api-Key';
	const REDACTED = 'REDACTED';
	const ACCEPT_SOAP = 'text/xml, application/soap+xml, application/xml;q=0.9, */*;q=0.1';
	const ACCEPT_WSDL = 'text/xml, application/xml;q=0.9, */*;q=0.1';

	const TEXT_DISABLED = 'Sending is disabled by the host application (EnableTryItOut is false).';
	const TEXT_NO_VERSION = 'This endpoint exposes no SOAP 1.1/1.2 envelope.';
	const TEXT_CURL_COPIED = 'Copied for bash/zsh. Header values for sensitive names are replaced with REDACTED. The request body is NOT redacted, so check it for credentials.';
	const TEXT_CURL_MANUAL = 'The clipboard is not available. The command is selected below, copy it manually. Sensitive header values are replaced with REDACTED, the request body is NOT redacted.';
	const TEXT_CODE_COPIED = 'Copied. Sensitive header values and the WS-Security password are replaced with REDACTED. The rest of the request body is NOT redacted, so check it for credentials.';
	const TEXT_CODE_MANUAL = 'The clipboard is not available. The code is selected below, copy it manually. Sensitive header values and the WS-Security password are replaced with REDACTED, the rest of the request body is NOT redacted.';
	const TEXT_AUTH_HINT = 'Authorization may be required: use the Authorize button.';
	const TEXT_AUTH_HINT_HEADERS = 'Authorization may be required: add credentials under "Custom request headers".';
	const NOTE_ACCEPTED = 'Accepted (no content). The endpoint took the message and sent no response body.';

	// The Authorize schemes. A "header" scheme sets one request header, the "envelope" scheme (WS-Security) changes a copy of the envelope when it is sent.
	const AUTH_SCHEMES = [
		{
			id: 'bearer',
			kind: 'header',
			title: 'Bearer token',
			short: 'Bearer',
			help: 'Adds the header "Authorization: Bearer <token>" to every request from this page.',
			fields: [{ key: 'token', label: 'Token', secret: true }]
		},
		{
			id: 'basic',
			kind: 'header',
			title: 'Basic authentication',
			short: 'Basic',
			help: 'Adds the header "Authorization: Basic ..." built from the user name and the password.',
			fields: [{ key: 'username', label: 'User name', secret: false }, { key: 'password', label: 'Password', secret: true }]
		},
		{
			id: 'apiKey',
			kind: 'header',
			title: 'API key',
			short: 'API key',
			help: 'Adds the key as a request header to every request from this page.',
			fields: [{ key: 'name', label: 'Header name', secret: false }, { key: 'value', label: 'Key', secret: true }]
		},
		{
			id: 'wsUsernameToken',
			kind: 'envelope',
			title: 'WS-Security UsernameToken',
			short: 'WS-Security',
			help: 'Adds a wsse:Security header with a UsernameToken to the SOAP Header of each request when it is sent. The text in the editor is not changed.',
			fields: [{ key: 'username', label: 'User name', secret: false }, { key: 'password', label: 'Password', secret: true }]
		}
	];

	const BLOCKED_TAGS = new Set(['script', 'style', 'iframe', 'object', 'embed', 'link', 'base', 'form', 'meta']);
	const PLAIN_ATTRIBUTES = new Set([
		'class', 'id', 'type', 'for', 'role', 'tabindex', 'hidden', 'title', 'placeholder', 'spellcheck',
		'autocomplete', 'name', 'value', 'rows', 'disabled', 'readonly', 'checked', 'open', 'scope'
	]);
	const DIRECTION_CLASSES = new Map([['in', 'dir-in'], ['out', 'dir-out'], ['ref', 'dir-ref']]);

	const REF_IDS = {
		appTitle: 'app-title',
		appSubtitle: 'app-subtitle',
		catalogSummary: 'catalog-summary',
		appDescription: 'app-description',
		sidebar: 'sidebar',
		sidebarToggle: 'sidebar-toggle',
		authOpen: 'auth-open',
		historyToggle: 'history-toggle',
		catalogReload: 'catalog-reload',
		themeSelect: 'theme-select',
		serviceSearch: 'service-search',
		serviceList: 'service-list',
		serviceListEmpty: 'service-list-empty',
		statusBar: 'status-bar',
		emptyState: 'empty-state',
		headersEditor: 'headers-editor',
		headersRows: 'headers-rows',
		headersAdd: 'headers-add',
		headersAuthNote: 'headers-auth-note',
		sendTimeout: 'send-timeout',
		sendTimeoutNote: 'send-timeout-note',
		historyPanel: 'history-panel',
		historyClear: 'history-clear',
		historyEmpty: 'history-empty',
		historyList: 'history-list',
		serviceView: 'service-view',
		serviceName: 'service-name',
		serviceMeta: 'service-meta',
		serviceBadges: 'service-badges',
		serviceDescription: 'service-description',
		serviceClient: 'service-client',
		serviceClientCommand: 'service-client-command',
		serviceClientCopy: 'service-client-copy',
		soapVersion: 'soap-version',
		soapVersion11: 'soap-version-11',
		soapVersion12: 'soap-version-12',
		tabOperations: 'tab-operations',
		tabWsdl: 'tab-wsdl',
		panelOperations: 'panel-operations',
		panelWsdl: 'panel-wsdl',
		operationsError: 'operations-error',
		operationSearch: 'operation-search',
		operationsExpand: 'operations-expand',
		operationsCollapse: 'operations-collapse',
		operationsList: 'operations-list',
		wsdlCopyUrl: 'wsdl-copy-url',
		wsdlDownload: 'wsdl-download',
		wsdlOpen: 'wsdl-open',
		wsdlStatus: 'wsdl-status',
		wsdlViews: 'wsdl-views',
		wsdlTabOutline: 'wsdl-tab-outline',
		wsdlTabRaw: 'wsdl-tab-raw',
		wsdlOutlinePanel: 'wsdl-outline-panel',
		wsdlOutlineFilter: 'wsdl-outline-filter',
		wsdlOutlineEmpty: 'wsdl-outline-empty',
		wsdlOutline: 'wsdl-outline',
		wsdlView: 'wsdl-view',
		authDialog: 'auth-dialog',
		authClose: 'auth-close',
		authNote: 'auth-note',
		authSections: 'auth-sections'
	};

	const refs = {};
	const app = {
		tryItOut: true,
		operationDetails: false,
		docExpansion: 'list',
		defaultVersion: null,
		apiKeyHeader: DEFAULT_API_KEY_HEADER,
		authSchemes: [],
		formEditor: true,
		sendTimeoutSeconds: DEFAULT_TIMEOUT_SECONDS,
		maxResponseBytes: MAX_BODY_BYTES,
		uiVersion: '',
		snippetKind: 'curl',
		wsdlView: 'outline',
		started: false,
		statusTimer: 0,
		services: [],
		byId: new Map(),
		entries: [],
		groups: [],
		states: new Map(),
		selected: null,
		nextOpId: 0,
		history: []
	};

	// The Authorize credentials, in memory only: scheme id -> { name, value } for a header scheme, or the options of the
	// WS-Security UsernameToken. Cleared by Log out and when the page is left. Never written to the URL or to any storage.
	const credentials = new Map();
	// The Authorize dialog sections that were built, by scheme id.
	const authSections = new Map();

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

	// Creates an element. Event handler and inline style attributes are refused, all other
	// attributes must be on the allow list. Children are nodes or plain text.
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

	function cacheRefs() {
		for (const key of Object.keys(REF_IDS)) {
			const node = document.getElementById(REF_IDS[key]);
			if (!node) {
				throw new Error('The page is missing the element #' + REF_IDS[key] + '.');
			}

			refs[key] = node;
		}
	}

	function setStatus(message, kind) {
		const bar = refs.statusBar;
		bar.textContent = message;
		bar.className = 'sbar ' + kind;
		bar.setAttribute('role', kind === 'err' ? 'alert' : 'status');
		bar.setAttribute('aria-live', kind === 'err' ? 'assertive' : 'polite');
		bar.hidden = false;
	}

	function clearStatus() {
		refs.statusBar.textContent = '';
		refs.statusBar.hidden = true;
	}

	// Shows a message that goes away by itself, unless something else was shown in the meantime.
	function announce(message) {
		setStatus(message, 'ok');
		window.clearTimeout(app.statusTimer);
		app.statusTimer = window.setTimeout(() => {
			if (refs.statusBar.textContent === message) {
				clearStatus();
			}
		}, STATUS_FADE_MS);
	}

	function text(value) {
		return typeof value === 'string' ? value : '';
	}

	// The optional scripts (Form view, XML view) must never break the page: a call that throws counts as "not available".
	function attempt(action, fallback) {
		try {
			return action();
		} catch (error) {
			return fallback;
		}
	}

	function flash(button, label) {
		const original = button.getAttribute('data-label') || button.textContent;
		button.setAttribute('data-label', original);
		button.textContent = label;
		window.clearTimeout(Number(button.getAttribute('data-timer')) || 0);
		button.setAttribute('data-timer', String(window.setTimeout(() => {
			button.textContent = original;
		}, 1800)));
	}

	function selectContents(node) {
		if (typeof node.select === 'function') {
			node.select();
			return;
		}

		const selection = window.getSelection();
		if (!selection) {
			return;
		}

		const range = document.createRange();
		range.selectNodeContents(node);
		selection.removeAllRanges();
		selection.addRange(range);
	}

	// Copies text with the async clipboard API only. When it is not available the target
	// is selected so the user can copy manually. Resolves to true when the copy worked.
	async function copyText(value, selectTarget) {
		try {
			if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
				await navigator.clipboard.writeText(value);
				return true;
			}
		} catch (error) {
			// Fall through to the manual selection below.
		}

		if (selectTarget) {
			selectContents(selectTarget);
		}

		return false;
	}

	// Keeps letters, digits, dot, underscore and hyphen, so a name from the server is a safe file name.
	function safeFileName(name, fallback) {
		const clean = text(name).replace(/[^A-Za-z0-9._-]/g, '_');
		return clean === '' ? fallback : clean;
	}

	// Saves text as a file: a Blob behind a temporary object URL, opened through a temporary anchor with the download attribute.
	function downloadText(fileName, content) {
		const url = URL.createObjectURL(new Blob([content], { type: 'application/xml;charset=utf-8' }));
		const anchor = document.createElement('a');
		anchor.href = url;
		anchor.download = fileName;
		anchor.hidden = true;
		document.body.appendChild(anchor);
		anchor.click();
		anchor.remove();
		window.setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS);
	}

	// Every endpoint URL used by the explorer goes through this check.
	function sameOriginUrl(path) {
		const url = new URL(path, window.location.origin);
		if (url.origin !== window.location.origin) {
			throw new Error('Refusing to call a different origin.');
		}

		return url.href;
	}

	function createDecoder(contentType) {
		const match = /charset\s*=\s*"?([A-Za-z0-9._:-]+)"?/i.exec(contentType || '');
		if (match) {
			try {
				return new TextDecoder(match[1]);
			} catch (error) {
				// Unknown charset label, use UTF-8.
			}
		}

		return new TextDecoder('utf-8');
	}

	// Reads at most maxBytes of a response body as text. Resolves to { text, truncated, bytes }, where bytes is what was read.
	async function readTextCapped(response, maxBytes) {
		if (!response.body) {
			return { text: '', truncated: false, bytes: 0 };
		}

		const reader = response.body.getReader();
		const decoder = createDecoder(response.headers.get('content-type'));
		const parts = [];
		let received = 0;
		let truncated = false;
		for (;;) {
			const step = await reader.read();
			if (step.done) {
				break;
			}

			let chunk = step.value;
			if (received + chunk.byteLength > maxBytes) {
				chunk = chunk.subarray(0, maxBytes - received);
				truncated = true;
			}

			received += chunk.byteLength;
			parts.push(decoder.decode(chunk, { stream: true }));
			if (truncated) {
				await reader.cancel();
				break;
			}
		}

		parts.push(decoder.decode());
		return { text: parts.join(''), truncated: truncated, bytes: received };
	}

	// ------------------------------------------------------------------
	// Catalog
	// ------------------------------------------------------------------

	function normalizeVersions(value) {
		const versions = [];
		if (Array.isArray(value)) {
			for (const item of value) {
				if ((item === '1.1' || item === '1.2') && versions.indexOf(item) === -1) {
					versions.push(item);
				}
			}
		}

		return versions.sort();
	}

	function isObject(value) {
		return value !== null && typeof value === 'object' && !Array.isArray(value);
	}

	function positiveInteger(value, fallback) {
		return Number.isInteger(value) && value > 0 ? value : fallback;
	}

	function normalizeParameter(parameter) {
		return {
			name: text(parameter.name),
			type: text(parameter.type),
			direction: text(parameter.direction),
			description: text(parameter.description)
		};
	}

	// The details of an operation are optional and exist only when the host exposes them. Each part is null when it is missing.
	function normalizeOperation(operation) {
		return {
			name: text(operation.name),
			soapAction: text(operation.soapAction),
			isOneWay: operation.isOneWay === true,
			contract: text(operation.contract),
			contractNamespace: text(operation.contractNamespace),
			description: text(operation.description),
			parameters: Array.isArray(operation.parameters) ? operation.parameters.filter(isObject).map(normalizeParameter) : null,
			returns: isObject(operation.returns) ? { type: text(operation.returns.type) } : null,
			faults: Array.isArray(operation.faults) ? operation.faults.filter((fault) => typeof fault === 'string' && fault !== '') : null,
			messageContract: isObject(operation.messageContract)
				? { request: operation.messageContract.request === true, response: operation.messageContract.response === true }
				: null
		};
	}

	function normalizeService(service) {
		const operations = Array.isArray(service.operations)
			? service.operations.filter((operation) => operation && typeof operation === 'object').map(normalizeOperation)
			: [];
		return {
			id: text(service.id),
			name: text(service.name) || text(service.path) || 'Service',
			path: text(service.path),
			wsdlUrl: text(service.wsdlUrl),
			serviceType: text(service.serviceType),
			serializer: text(service.serializer),
			customSerializerType: text(service.customSerializerType),
			description: text(service.description),
			soapVersions: normalizeVersions(service.soapVersions),
			caseInsensitivePath: service.caseInsensitivePath === true,
			operations: operations,
			operationsError: text(service.operationsError)
		};
	}

	// Validates the shape of the API payload. Returns null when it is not a catalog.
	function normalizeCatalog(data) {
		if (!data || typeof data !== 'object' || !Array.isArray(data.services)) {
			return null;
		}

		return {
			title: text(data.title),
			description: text(data.description),
			tryItOut: data.tryItOutEnabled !== false,
			operationDetails: data.operationDetailsEnabled === true,
			basePath: text(data.basePath),
			defaultVersion: data.defaultSoapVersion === '1.1' || data.defaultSoapVersion === '1.2' ? data.defaultSoapVersion : null,
			docExpansion: data.docExpansion === 'full' ? 'full' : 'list',
			authSchemes: Array.isArray(data.authSchemes) ? data.authSchemes.filter((scheme) => typeof scheme === 'string') : [],
			apiKeyHeaderName: text(data.apiKeyHeaderName),
			stylesheetUrl: text(data.stylesheetUrl),
			formEditor: data.formEditorEnabled !== false,
			sendTimeoutSeconds: positiveInteger(data.sendTimeoutSeconds, DEFAULT_TIMEOUT_SECONDS),
			maxResponseBytes: positiveInteger(data.maxResponseBytes, MAX_BODY_BYTES),
			uiVersion: text(data.uiVersion),
			services: data.services.filter((service) => service && typeof service === 'object').map(normalizeService)
		};
	}

	// The SOAP version a service starts with: the one the host prefers when the endpoint exposes it, otherwise 1.1, otherwise the first.
	function initialVersion(service) {
		const versions = service.soapVersions;
		if (app.defaultVersion && versions.indexOf(app.defaultVersion) !== -1) {
			return app.defaultVersion;
		}

		return versions.indexOf('1.1') !== -1 ? '1.1' : (versions.length > 0 ? versions[0] : null);
	}

	function stateFor(service) {
		let state = app.states.get(service);
		if (!state) {
			state = {
				service: service,
				version: initialVersion(service),
				wsdl: { status: 'idle', model: null, text: '', display: '', message: '', httpStatus: 0, headersKey: '', ready: null, outline: undefined, outlineTree: null },
				container: null,
				warnBar: null,
				noMatch: null,
				ops: []
			};
			app.states.set(service, state);
		}

		return state;
	}

	// ------------------------------------------------------------------
	// WSDL loading
	// ------------------------------------------------------------------

	// The operation details (parameters) exist only when the host exposes them. They are what a sample is built from without the WSDL.
	function hasOperationDetails(operation) {
		return Array.isArray(operation.parameters);
	}

	// Says what the explorer does instead of using the WSDL. An endpoint without a SOAP 1.1/1.2 envelope has no generic envelope either.
	function fallbackSuffix(state) {
		if (state.service.soapVersions.length === 0) {
			return ' This endpoint exposes no SOAP 1.1/1.2 envelope, so it cannot be called from the explorer.';
		}

		return state.service.operations.some(hasOperationDetails)
			? ' Samples are built from the operation details.'
			: ' A generic envelope is used.';
	}

	function wsdlStatusMessage(status) {
		switch (status) {
			case 401:
			case 403:
				return 'The endpoint refused the WSDL request (HTTP ' + status + '). Either GET is disabled (HttpGetEnabled/HttpsGetEnabled is false), or the endpoint needs credentials: use the Authorize button or add them under "Custom request headers", then press Reset sample. The WSDL is also requested again when Authorize changes.';
			case 404:
				return 'The WSDL URL was not found (HTTP 404). If the endpoint sits behind app.Map(...), set SoapUiOptions.EndpointPathPrefix.';
			case 500:
				return 'The server could not generate a WSDL (for example the endpoint uses MessageVersion.None, or the service has no ServiceContract).';
			default:
				return 'The WSDL request answered HTTP ' + status + '.';
		}
	}

	// The WSDL is switched off when the request is refused or the URL is not found. Other failures are not blamed on the endpoint.
	function wsdlUnavailable(wsdl) {
		return wsdl.status === 'error' && (wsdl.httpStatus === 403 || wsdl.httpStatus === 404);
	}

	function refreshServiceEntry(state) {
		const entry = app.entries.find((candidate) => candidate.service === state.service);
		if (entry) {
			entry.wsdlBadge.hidden = !wsdlUnavailable(state.wsdl);
		}
	}

	function refreshWsdlPanel(state) {
		const wsdl = state.wsdl;
		if (state.warnBar) {
			state.warnBar.textContent = wsdl.status === 'error' ? wsdl.message : '';
			state.warnBar.hidden = wsdl.status !== 'error';
		}

		refreshServiceEntry(state);
		if (app.selected !== state) {
			return;
		}

		let status = '';
		if (wsdl.status === 'loading') {
			status = 'Loading the WSDL...';
		} else if (wsdl.status === 'ok') {
			status = 'WSDL loaded from ' + state.service.wsdlUrl;
		} else if (wsdl.message) {
			status = wsdl.message;
		}

		refs.wsdlStatus.textContent = status;
		refs.wsdlStatus.className = wsdl.status === 'error' ? 'sbar warn' : 'muted';
		refs.wsdlView.textContent = wsdl.display;
		refs.wsdlDownload.disabled = wsdl.text === '';
		refreshWsdlViews(state);
	}

	function failWsdl(state, message) {
		state.wsdl.status = 'error';
		state.wsdl.message = message + fallbackSuffix(state);
	}

	// The WSDL request carries the same headers as a send (Authorize, then the custom headers), so endpoints that need an
	// Authorization header or an API key can serve their WSDL. Invalid custom rows are skipped here: they are reported when the user sends.
	async function loadWsdl(state) {
		const wsdl = state.wsdl;
		const extra = wsdlRequestHeaders();
		wsdl.status = 'loading';
		wsdl.model = null;
		wsdl.text = '';
		wsdl.display = '';
		wsdl.message = '';
		wsdl.httpStatus = 0;
		wsdl.outline = undefined;
		wsdl.outlineTree = null;
		wsdl.headersKey = headersKey(extra);
		refreshWsdlPanel(state);
		try {
			const headers = new Headers();
			headers.set('Accept', ACCEPT_WSDL);
			for (const header of extra) {
				headers.set(header.name, header.value);
			}

			const response = await fetch(sameOriginUrl(state.service.wsdlUrl), {
				method: 'GET',
				headers: headers,
				credentials: 'same-origin',
				cache: 'no-store',
				redirect: 'error',
				referrerPolicy: 'no-referrer'
			});
			if (!response.ok) {
				wsdl.httpStatus = response.status;
				failWsdl(state, wsdlStatusMessage(response.status));
				return;
			}

			const result = await readTextCapped(response, MAX_BODY_BYTES);
			if (result.truncated) {
				failWsdl(state, 'The WSDL is larger than 5 MB and was not processed.');
				return;
			}

			wsdl.text = result.text;
			let display = result.text;
			if (display.length <= MAX_PRETTY_CHARS && !Wsdl.hasDoctype(display)) {
				try {
					display = Wsdl.prettyPrintXml(display);
				} catch (error) {
					display = result.text;
				}
			}

			wsdl.display = display;
			const parsed = Wsdl.parse(result.text);
			if (parsed.ok) {
				wsdl.model = parsed.model;
				wsdl.status = 'ok';
				wsdl.message = '';
			} else {
				failWsdl(state, 'The WSDL could not be used: ' + parsed.error);
			}
		} catch (error) {
			failWsdl(state, 'The WSDL request failed (network error, or the endpoint redirected).');
		} finally {
			refreshWsdlPanel(state);
		}
	}

	// Rebuilds the sample envelopes that were generated without the WSDL and have not been edited since.
	function regenerateGeneratedEnvelopes(state) {
		for (const ctx of state.ops) {
			if (ctx.generated && !ctx.edited && !ctx.busy) {
				generateEnvelope(ctx, false);
			}
		}
	}

	// Loads the WSDL once per service. The returned promise always resolves.
	// A failed load is not final: it is tried again when the request headers (Authorize and custom headers) changed since the
	// failed request, or when retryFailed is true (Reset sample, SOAP version switch). A WSDL that loaded is kept for the page session.
	function ensureWsdl(state, retryFailed) {
		const wsdl = state.wsdl;
		const failed = wsdl.status === 'error';
		const retry = failed && (retryFailed === true || wsdl.headersKey !== headersKey(wsdlRequestHeaders()));
		if (!wsdl.ready || retry) {
			wsdl.ready = loadWsdl(state)
				.catch(() => {
					failWsdl(state, 'The WSDL could not be loaded.');
					refreshWsdlPanel(state);
				})
				.then(() => {
					if (failed && wsdl.status === 'ok') {
						regenerateGeneratedEnvelopes(state);
					}

					refreshViewSwitches(state);
				});
		}

		return wsdl.ready;
	}

	// ------------------------------------------------------------------
	// WSDL outline (the Outline | Raw switch of the WSDL tab)
	// ------------------------------------------------------------------

	function list(value) {
		return Array.isArray(value) ? value : [];
	}

	// The items of a list that are objects. The outline comes from a script that is trusted, but a hole in it must not break the page.
	function objects(value) {
		return list(value).filter(isObject);
	}

	function mono(value) {
		return el('span', { class: 'mono' }, text(value));
	}

	function badge(label) {
		return label === '' ? null : el('span', { class: 'badge' }, label);
	}

	// The outline of the WSDL of a service, or null when there is none: the WSDL did not load, or buildWsdlOutline is missing or failed.
	// It is built once per WSDL.
	function outlineFor(state) {
		const wsdl = state.wsdl;
		if (wsdl.status !== 'ok' || !hasWsdlFunction('buildWsdlOutline')) {
			return null;
		}

		if (wsdl.outline === undefined) {
			const result = attempt(() => Wsdl.buildWsdlOutline(wsdl.text), null);
			wsdl.outline = result && result.ok === true && isObject(result.outline) ? result.outline : null;
		}

		return wsdl.outline;
	}

	// A node of the outline that the filter can hide: element is what is hidden, words are what the filter looks at, children are the nodes below.
	function outlineNode(element, words, children) {
		return { el: element, text: words.map(text).join(' ').toLowerCase(), children: children || [] };
	}

	// A table with one node per row. Each row is { cells, words }. The first cell is the row header.
	function outlineTable(columns, rows) {
		const nodes = rows.map((row) => outlineNode(
			el('tr', null, row.cells.map((cell, index) => (index === 0 ? el('th', { scope: 'row' }, cell) : el('td', null, cell)))),
			row.words));
		const element = el('div', { class: 'table-wrap' }, el('table', { class: 'ol-table' }, [
			el('thead', null, el('tr', null, columns.map((label) => el('th', { scope: 'col' }, label)))),
			el('tbody', null, nodes.map((node) => node.el))
		]));
		return { element: element, nodes: nodes };
	}

	function outlineFacts(pairs) {
		const shown = pairs.filter((pair) => text(pair[1]) !== '');
		return shown.length === 0 ? null : el('dl', { class: 'ol-facts' }, shown.map((pair) => el('div', null, [el('dt', null, pair[0]), el('dd', null, mono(pair[1]))])));
	}

	// A collapsible group of the outline. It starts open when it is small. The words are the names that make the whole group match.
	function outlineGroup(title, badges, body, open, words, children) {
		const element = el('details', { class: 'ol-item', open: open }, [
			el('summary', null, [title, badges]),
			el('div', { class: 'ol-body' }, body)
		]);
		return outlineNode(element, words, children);
	}

	function outlineSection(title, items, count) {
		const element = el('details', { class: 'ol-section', open: true }, [
			el('summary', null, [title, badge(String(count))]),
			el('div', { class: 'ol-body' }, items.map((item) => item.el))
		]);
		return outlineNode(element, [], items);
	}

	function serviceNodes(services) {
		return services.map((service) => {
			const table = outlineTable(['Port', 'Binding', 'Address', 'SOAP'], objects(service.ports).map((port) => ({
				cells: [mono(port.name), mono(port.binding), mono(port.address), badge(port.soapVersion ? 'SOAP ' + text(port.soapVersion) : '')],
				words: [port.name, port.binding, port.address]
			})));
			return outlineGroup(text(service.name), null, table.element, true, [service.name], table.nodes);
		});
	}

	function bindingNodes(bindings) {
		return bindings.map((binding) => {
			// The binding style (document or rpc) is read by destructuring: it is data of the WSDL, not an element style.
			const { name, type, transport, style, soapVersion } = binding;
			const table = outlineTable(['Operation', 'SOAPAction'], objects(binding.operations).map((operation) => ({
				cells: [mono(operation.name), mono(operation.soapAction)],
				words: [operation.name, operation.soapAction]
			})));
			const facts = outlineFacts([['Port type', type], ['Transport', transport], ['Style', style]]);
			const badges = [soapVersion ? badge('SOAP ' + text(soapVersion)) : null, badge(text(style))];
			return outlineGroup(text(name), badges, [facts, table.element], bindings.length <= 3, [name, type, transport], table.nodes);
		});
	}

	function portTypeNodes(portTypes) {
		return portTypes.map((portType) => {
			const table = outlineTable(['Operation', 'Input', 'Output', 'Faults'], objects(portType.operations).map((operation) => ({
				cells: [mono(operation.name), mono(operation.input), mono(operation.output), mono(list(operation.faults).join(', '))],
				words: [operation.name, operation.input, operation.output].concat(list(operation.faults))
			})));
			return outlineGroup(text(portType.name), null, table.element, portTypes.length <= 3, [portType.name], table.nodes);
		});
	}

	function messageNodes(messages) {
		return messages.map((message) => {
			const table = outlineTable(['Part', 'Element', 'Type'], objects(message.parts).map((part) => ({
				cells: [mono(part.name), mono(part.element), mono(part.type)],
				words: [part.name, part.element, part.type]
			})));
			return outlineGroup(text(message.name), null, table.element, messages.length <= 3, [message.name], table.nodes);
		});
	}

	// The names of the schema, in up to three groups. The groups have no words of their own, only the names inside them match.
	function typeNodes(types) {
		const groups = [
			['Elements', list(types.elements).map((name) => ({ name: name, extra: '' }))],
			['Complex types', list(types.complexTypes).map((name) => ({ name: name, extra: '' }))],
			['Simple types', objects(types.simpleTypes).map((type) => ({ name: type.name, extra: Number.isInteger(type.enumCount) && type.enumCount > 0 ? plural(type.enumCount, 'enum value') : '' }))]
		];
		return groups.filter((group) => group[1].length > 0).map((group) => {
			const names = group[1].map((entry) => outlineNode(
				el('li', null, [mono(entry.name), entry.extra === '' ? null : el('span', { class: 'muted' }, ' ' + entry.extra)]),
				[entry.name]));
			const body = el('ul', { class: 'ol-names' }, names.map((node) => node.el));
			return outlineGroup(group[0], badge(String(names.length)), body, names.length <= 30, [], names);
		});
	}

	function warningsBlock(warnings) {
		const items = warnings.filter((warning) => typeof warning === 'string' && warning !== '');
		return items.length === 0 ? null : el('div', { class: 'sbar warn' }, [
			el('p', null, 'Warnings'),
			el('ul', { class: 'ol-warnings' }, items.map((warning) => el('li', null, warning)))
		]);
	}

	// The DOM of an outline: the sections that have something in them, and a tree of nodes for the filter.
	function buildOutlineTree(outline) {
		const types = typeNodes(isObject(outline.types) ? outline.types : {});
		const sections = [
			['Services', serviceNodes(objects(outline.services)), null],
			['Bindings', bindingNodes(objects(outline.bindings)), null],
			['Port types', portTypeNodes(objects(outline.portTypes)), null],
			['Messages', messageNodes(objects(outline.messages)), null],
			['Types', types, types.reduce((total, group) => total + group.children.length, 0)]
		].filter((entry) => entry[1].length > 0).map((entry) => outlineSection(entry[0], entry[1], entry[2] === null ? entry[1].length : entry[2]));
		const root = el('div', { class: 'ol-root' }, [
			text(outline.targetNamespace) === '' ? null : el('p', { class: 'muted' }, ['Target namespace: ', mono(outline.targetNamespace)]),
			sections.map((section) => section.el),
			warningsBlock(list(outline.warnings))
		]);
		return { root: root, sections: sections };
	}

	// Shows the nodes that have the query in their words. A node that matches shows everything below it. A node that does not match is
	// shown only when something below it does, and it is opened. Returns whether the node is shown.
	function filterOutlineNode(node, query, forced) {
		const matches = query === '' || node.text.indexOf(query) !== -1;
		let below = false;
		for (const child of node.children) {
			below = filterOutlineNode(child, query, forced || (query !== '' && matches)) || below;
		}

		const shown = forced || matches || below;
		node.el.hidden = !shown;
		if (shown && query !== '' && node.children.length > 0) {
			node.el.open = true;
		}

		return shown;
	}

	function applyOutlineFilter() {
		const tree = app.selected ? app.selected.wsdl.outlineTree : null;
		if (!tree) {
			return;
		}

		const query = refs.wsdlOutlineFilter.value.trim().toLowerCase();
		const shown = tree.sections.map((section) => filterOutlineNode(section, query, false));
		refs.wsdlOutlineEmpty.hidden = query === '' || shown.some(Boolean);
	}

	function mountOutline(state, outline) {
		const wsdl = state.wsdl;
		if (!wsdl.outlineTree) {
			wsdl.outlineTree = buildOutlineTree(outline);
		}

		if (refs.wsdlOutline.firstChild !== wsdl.outlineTree.root) {
			refs.wsdlOutline.replaceChildren(wsdl.outlineTree.root);
			refs.wsdlOutlineFilter.value = '';
			applyOutlineFilter();
		}
	}

	// Shows the WSDL as an outline or as raw text. The switch exists only when an outline could be built. Nothing is done while the
	// WSDL tab is not shown: the outline is built when the tab is opened.
	function refreshWsdlViews(state) {
		if (!state || state !== app.selected || refs.panelWsdl.hidden) {
			return;
		}

		const outline = outlineFor(state);
		const available = outline !== null;
		const outlineShown = available && app.wsdlView === 'outline';
		refs.wsdlViews.hidden = !available;
		setPaneRole(refs.wsdlOutlinePanel, refs.wsdlTabOutline, available);
		setPaneRole(refs.wsdlView, refs.wsdlTabRaw, available);
		for (const [tab, selected] of [[refs.wsdlTabOutline, outlineShown], [refs.wsdlTabRaw, !outlineShown]]) {
			tab.setAttribute('aria-selected', String(selected));
			tab.setAttribute('tabindex', selected ? '0' : '-1');
		}

		refs.wsdlOutlinePanel.hidden = !outlineShown;
		refs.wsdlView.hidden = outlineShown || state.wsdl.display === '';
		if (outlineShown) {
			mountOutline(state, outline);
		}
	}

	function selectWsdlView(tab, moveFocus) {
		app.wsdlView = tab === refs.wsdlTabRaw ? 'raw' : 'outline';
		refreshWsdlViews(app.selected);
		if (moveFocus) {
			tab.focus();
		}
	}

	// ------------------------------------------------------------------
	// Custom request headers (in memory only)
	// ------------------------------------------------------------------

	function applyValueMode(nameInput, valueInput) {
		if (Wsdl.isSensitiveHeader(nameInput.value.trim())) {
			valueInput.setAttribute('type', 'password');
			valueInput.setAttribute('autocomplete', 'new-password');
		} else {
			valueInput.setAttribute('type', 'text');
			valueInput.setAttribute('autocomplete', 'off');
		}
	}

	function addHeaderRow() {
		const nameInput = el('input', {
			class: 'input header-name mono',
			type: 'text',
			autocomplete: 'off',
			spellcheck: 'false',
			placeholder: 'Header name',
			'aria-label': 'Header name',
			'data-lpignore': 'true',
			'data-1p-ignore': 'true'
		});
		const valueInput = el('input', {
			class: 'input header-value mono',
			type: 'text',
			autocomplete: 'off',
			spellcheck: 'false',
			placeholder: 'Header value',
			'aria-label': 'Header value',
			'data-lpignore': 'true',
			'data-1p-ignore': 'true'
		});
		const remove = el('button', { class: 'btn header-remove', type: 'button', 'aria-label': 'Remove this header' }, 'Remove');
		const error = el('p', { class: 'header-error', role: 'alert', hidden: true });
		const row = el('div', { class: 'header-row' }, [nameInput, valueInput, remove, error]);

		const clearError = () => {
			error.textContent = '';
			error.hidden = true;
		};
		nameInput.addEventListener('input', () => {
			applyValueMode(nameInput, valueInput);
			clearError();
			refreshAuthNotes();
		});
		valueInput.addEventListener('input', clearError);
		remove.addEventListener('click', () => {
			row.remove();
			refreshAuthNotes();
			refs.headersAdd.focus();
		});

		refs.headersRows.appendChild(row);
		return nameInput;
	}

	// Reads and validates the header rows. Returns { ok: true, headers } or { ok: false, error }.
	// In quiet mode the page is left untouched (no inline errors) and invalid rows are skipped: the result is always
	// { ok: true, headers } with the valid rows only. The WSDL request uses this mode.
	function readCustomHeaders(quiet) {
		const used = new Set();
		const headers = [];
		let firstError = '';
		for (const row of refs.headersRows.querySelectorAll('.header-row')) {
			const nameInput = row.querySelector('.header-name');
			const valueInput = row.querySelector('.header-value');
			const errorNode = row.querySelector('.header-error');
			if (!quiet) {
				errorNode.textContent = '';
				errorNode.hidden = true;
			}

			if (nameInput.value.trim() === '' && valueInput.value === '') {
				continue;
			}

			const check = Wsdl.validateHeader(nameInput.value, valueInput.value, used);
			let message = check.ok ? '' : check.error;
			if (check.ok) {
				try {
					new Headers().set(check.name, check.value);
				} catch (error) {
					message = 'The browser rejected this header.';
				}
			}

			if (message !== '') {
				if (!quiet) {
					errorNode.textContent = message;
					errorNode.hidden = false;
				}

				if (firstError === '') {
					firstError = message;
				}
			} else {
				used.add(check.name.toLowerCase());
				headers.push({ name: check.name, value: check.value });
			}
		}

		if (firstError !== '' && !quiet) {
			refs.headersEditor.open = true;
			return { ok: false, error: 'Fix the custom request headers first. ' + firstError };
		}

		return { ok: true, headers: headers };
	}

	// A comparable summary of the valid custom headers, kept in memory only. It tells ensureWsdl whether the headers
	// changed since the WSDL request was made.
	function headersKey(headers) {
		return headers.map((header) => header.name.toLowerCase() + '\u0000' + header.value).join('\u0001');
	}

	// ------------------------------------------------------------------
	// Request timeout
	// ------------------------------------------------------------------

	// The timeout of a send in whole seconds, and a note when the typed value had to be corrected. The host sets the largest value,
	// which is also the default. A value that is not a number gives the default, a value out of range is clamped.
	function readTimeout() {
		const max = app.sendTimeoutSeconds;
		const raw = refs.sendTimeout.value.trim();
		const value = Number(raw);
		if (raw === '' || !Number.isFinite(value)) {
			return { seconds: max, note: 'Enter a whole number of seconds. The default of ' + max + ' s is used.' };
		}

		const seconds = Math.min(max, Math.max(MIN_TIMEOUT_SECONDS, Math.round(value)));
		return { seconds: seconds, note: seconds === value ? '' : 'The value was corrected to ' + seconds + ' s.' };
	}

	function refreshTimeoutNote() {
		const note = readTimeout().note;
		const help = 'Applies to each request sent from this page. Whole seconds, from ' + MIN_TIMEOUT_SECONDS + ' to ' + app.sendTimeoutSeconds + '.';
		refs.sendTimeoutNote.textContent = note === '' ? help : note;
		refs.sendTimeoutNote.classList.toggle('warn', note !== '');
	}

	// ------------------------------------------------------------------
	// Authorize (credentials in memory only)
	// ------------------------------------------------------------------

	function hasWsdlFunction(name) {
		return typeof Wsdl[name] === 'function';
	}

	// A scheme is offered only when wsdl-parser.js has the functions it needs, so an older script degrades to no Authorize button.
	function schemeSupported(id) {
		return id === 'wsUsernameToken'
			? hasWsdlFunction('buildWsSecurityHeader') && hasWsdlFunction('applyWsSecurity')
			: hasWsdlFunction('buildAuthHeader');
	}

	// The schemes to offer: the ones the host enables that this page can build. An empty list hides the Authorize button.
	function offeredSchemes(requested) {
		return AUTH_SCHEMES.filter((scheme) => requested.indexOf(scheme.id) !== -1 && schemeSupported(scheme.id));
	}

	function activeSchemes() {
		return AUTH_SCHEMES.filter((scheme) => credentials.has(scheme.id));
	}

	function isAuthorized() {
		return credentials.size > 0;
	}

	// The headers set by the active header schemes (Bearer, Basic, API key).
	function authHeaders() {
		return activeSchemes().filter((scheme) => scheme.kind === 'header').map((scheme) => credentials.get(scheme.id));
	}

	// The Authorize headers first, then the custom ones. A custom header with the same name wins, so an explicit choice is never
	// overridden. With redact the Authorize values are masked: code snippets never show them, whatever the header is called.
	function withAuthHeaders(custom, redact) {
		const names = new Set(custom.map((header) => header.name.toLowerCase()));
		const added = authHeaders()
			.filter((header) => !names.has(header.name.toLowerCase()))
			.map((header) => ({ name: header.name, value: redact ? REDACTED : header.value }));
		return added.concat(custom);
	}

	// The headers of the WSDL request. Invalid custom rows are skipped.
	function wsdlRequestHeaders() {
		return withAuthHeaders(readCustomHeaders(true).headers, false);
	}

	function isoNow() {
		return new Date().toISOString().replace(/\.\d+Z$/, 'Z');
	}

	function randomNonceBytes() {
		return Array.from(window.crypto.getRandomValues(new Uint8Array(16)));
	}

	// Adds the WS-Security UsernameToken to a COPY of the envelope text. Each call makes a fresh Created and Nonce. Returns
	// { ok: true, text } or { ok: false, error }. With redact the password is the literal REDACTED, for code snippets.
	function withWsSecurity(envelope, version, redact) {
		const options = credentials.get('wsUsernameToken');
		if (!options) {
			return { ok: true, text: envelope };
		}

		const header = Wsdl.buildWsSecurityHeader(Object.assign({ redact: redact }, options), isoNow(), randomNonceBytes());
		return header.ok ? Wsdl.applyWsSecurity(envelope, header.xml, version) : header;
	}

	// The note that says an explicit custom header replaced one of the Authorize headers.
	function refreshAuthNotes() {
		const names = new Set(readCustomHeaders(true).headers.map((header) => header.name.toLowerCase()));
		const clashes = authHeaders().filter((header) => names.has(header.name.toLowerCase())).map((header) => header.name);
		const note = clashes.length === 0 ? '' : 'A custom request header overrides the header set by Authorize: ' + clashes.join(', ') + '.';
		for (const node of [refs.authNote, refs.headersAuthNote]) {
			node.textContent = note;
			node.hidden = note === '';
		}
	}

	function refreshAuthNote(ctx) {
		const active = activeSchemes();
		ctx.authNote.hidden = active.length === 0;
		ctx.authNote.textContent = active.length === 0
			? ''
			: 'Authorize is active (' + active.map((scheme) => scheme.short).join(', ') + '). It is added when the request is sent and does not change the editor text.';
	}

	function forEachBuiltOperation(action) {
		for (const state of app.states.values()) {
			for (const ctx of state.ops) {
				if (ctx.body) {
					action(ctx);
				}
			}
		}
	}

	function setSectionMessage(section, message, kind) {
		section.status.textContent = message;
		section.status.className = 'auth-status ' + kind;
	}

	function updateAuthSection(section) {
		const active = credentials.has(section.scheme.id);
		section.logout.disabled = !active;
		setSectionMessage(section, active ? 'Authorized. Kept in memory only.' : 'Not authorized.', active ? 'ok' : '');
	}

	// Everything on the page that shows whether Authorize is active.
	function refreshAuthState() {
		const active = activeSchemes();
		refs.authOpen.textContent = active.length === 0 ? 'Authorize' : 'Authorized (' + active.map((scheme) => scheme.short).join(', ') + ')';
		refs.authOpen.classList.toggle('is-active', active.length > 0);
		for (const section of authSections.values()) {
			updateAuthSection(section);
		}

		refreshAuthNotes();
		forEachBuiltOperation(refreshAuthNote);
	}

	function readSectionValues(section) {
		const values = {};
		section.inputs.forEach((input, key) => {
			values[key] = input.value;
		});
		return values;
	}

	// Secrets are not kept in the dialog once they are authorized: only the module level credentials hold them.
	function clearSecrets(section) {
		for (const field of section.scheme.fields) {
			if (field.secret) {
				section.inputs.get(field.key).value = '';
			}
		}
	}

	// buildAuthHeader holds the header to the limits of a custom header (no owned or forbidden names, printable ASCII). The
	// browser has the last word, as it does for the custom headers.
	function acceptableHeader(header) {
		try {
			new Headers().set(header.name, header.value);
		} catch (error) {
			return { ok: false, error: 'The browser rejected this header.' };
		}

		return { ok: true, credential: { name: header.name, value: header.value } };
	}

	function readHeaderCredential(section) {
		const built = Wsdl.buildAuthHeader(section.scheme.id, readSectionValues(section));
		return built.ok ? acceptableHeader(built.header) : built;
	}

	function readWsCredential(section) {
		const values = readSectionValues(section);
		const options = {
			username: values.username,
			password: values.password,
			passwordType: section.type.value === 'digest' ? 'digest' : 'text',
			includeCreated: section.created.checked,
			includeNonce: section.nonce.checked
		};
		// A dry run with the password masked: it reports a missing user name or an unusable option now, not at the first send.
		const check = Wsdl.buildWsSecurityHeader(Object.assign({ redact: true }, options), isoNow(), randomNonceBytes());
		return check.ok ? { ok: true, credential: options } : check;
	}

	// Stores a credential. Two header schemes cannot set the same header, so the one that was active with that name is logged out.
	// Returns the schemes that were replaced.
	function storeCredential(scheme, credential) {
		const replaced = [];
		if (scheme.kind === 'header') {
			for (const other of activeSchemes()) {
				const active = other.kind === 'header' && other !== scheme ? credentials.get(other.id) : null;
				if (active && active.name.toLowerCase() === credential.name.toLowerCase()) {
					credentials.delete(other.id);
					clearSecrets(authSections.get(other.id));
					replaced.push(other);
				}
			}
		}

		credentials.set(scheme.id, credential);
		return replaced;
	}

	// A WSDL that was refused (HTTP 401 or 403) is requested again when Authorize changes. ensureWsdl does it once per change of headers.
	function retryWsdlAfterAuthChange() {
		const state = app.selected;
		if (state && state.wsdl.status === 'error' && (state.wsdl.httpStatus === 401 || state.wsdl.httpStatus === 403)) {
			ensureWsdl(state, false);
		}
	}

	function authorizeScheme(section) {
		const scheme = section.scheme;
		const result = scheme.kind === 'envelope' ? readWsCredential(section) : readHeaderCredential(section);
		if (!result.ok) {
			setSectionMessage(section, result.error, 'err');
			return;
		}

		const replaced = storeCredential(scheme, result.credential);
		clearSecrets(section);
		refreshAuthState();
		for (const other of replaced) {
			setSectionMessage(authSections.get(other.id), 'Logged out: only one scheme can set the ' + result.credential.name + ' header.', 'warn');
		}

		retryWsdlAfterAuthChange();
	}

	function logoutScheme(section) {
		credentials.delete(section.scheme.id);
		clearSecrets(section);
		refreshAuthState();
		retryWsdlAfterAuthChange();
	}

	function logoutAll() {
		credentials.clear();
		for (const section of authSections.values()) {
			clearSecrets(section);
		}

		refreshAuthState();
	}

	// Digest needs both Created and Nonce, so the two options are set and locked while it is chosen.
	function syncDigestOptions(section) {
		const digest = section.type.value === 'digest';
		for (const box of [section.created, section.nonce]) {
			if (digest) {
				box.checked = true;
			}

			box.disabled = digest;
		}
	}

	function buildWsOptions(section) {
		const prefix = 'auth-' + section.scheme.id;
		section.type = el('select', { class: 'input input-small', id: prefix + '-type' }, [
			el('option', { value: 'text' }, 'Plain text password'),
			el('option', { value: 'digest' }, 'Password digest (SHA-1, needs Created and Nonce)')
		]);
		section.created = el('input', { type: 'checkbox', id: prefix + '-created' });
		section.nonce = el('input', { type: 'checkbox', id: prefix + '-nonce' });
		section.type.addEventListener('change', () => syncDigestOptions(section));
		return el('div', { class: 'auth-options' }, [
			el('label', { class: 'lbl', for: prefix + '-type' }, 'Password type'),
			section.type,
			el('label', { class: 'check' }, [section.created, 'Include Created (timestamp)']),
			el('label', { class: 'check' }, [section.nonce, 'Include Nonce'])
		]);
	}

	function buildAuthField(section, field) {
		const input = el('input', {
			class: 'input mono',
			id: 'auth-' + section.scheme.id + '-' + field.key,
			type: field.secret ? 'password' : 'text',
			autocomplete: 'off',
			spellcheck: 'false',
			'data-lpignore': 'true',
			'data-1p-ignore': 'true'
		});
		section.inputs.set(field.key, input);
		input.addEventListener('keydown', (event) => {
			if (event.key === 'Enter') {
				event.preventDefault();
				authorizeScheme(section);
			}
		});
		return el('div', { class: 'field' }, [el('label', { class: 'lbl', for: input.id }, field.label), input]);
	}

	function buildAuthSection(scheme) {
		const titleId = 'auth-' + scheme.id + '-title';
		const section = {
			scheme: scheme,
			inputs: new Map(),
			authorize: el('button', { class: 'btn btn-go', type: 'button' }, 'Authorize'),
			logout: el('button', { class: 'btn', type: 'button' }, 'Log out'),
			status: el('p', { class: 'auth-status', role: 'status', 'aria-live': 'polite' })
		};
		const fields = scheme.fields.map((field) => buildAuthField(section, field));
		if (scheme.id === 'apiKey') {
			section.inputs.get('name').value = app.apiKeyHeader;
		}

		section.root = el('section', { class: 'auth-section', 'aria-labelledby': titleId }, [
			el('h3', { id: titleId, class: 'auth-title' }, scheme.title),
			el('p', { class: 'muted' }, scheme.help),
			fields,
			scheme.kind === 'envelope' ? buildWsOptions(section) : null,
			el('div', { class: 'auth-buttons' }, [section.authorize, section.logout]),
			section.status
		]);
		section.authorize.addEventListener('click', () => authorizeScheme(section));
		section.logout.addEventListener('click', () => logoutScheme(section));
		return section;
	}

	// The native dialog is modal and handles Esc and the focus trap. Without it the dialog is shown as a plain block.
	function dialogIsNative() {
		return typeof refs.authDialog.showModal === 'function';
	}

	function openAuthDialog() {
		refreshAuthNotes();
		if (dialogIsNative()) {
			if (!refs.authDialog.open) {
				refs.authDialog.showModal();
			}
		} else {
			refs.authDialog.setAttribute('open', '');
		}

		const first = refs.authSections.querySelector('input, select');
		if (first) {
			first.focus();
		}
	}

	function closeAuthDialog() {
		if (dialogIsNative()) {
			refs.authDialog.close();
		} else {
			refs.authDialog.removeAttribute('open');
		}

		refs.authOpen.focus();
	}

	function wireAuthDialog() {
		refs.authOpen.addEventListener('click', openAuthDialog);
		refs.authClose.addEventListener('click', closeAuthDialog);
		// Esc closes a native dialog by itself. The focus goes back to the button that opened it.
		refs.authDialog.addEventListener('close', () => refs.authOpen.focus());
		refs.authDialog.addEventListener('click', (event) => {
			// The dialog has no padding of its own, so a click on the dialog element itself is a click on the backdrop.
			if (event.target === refs.authDialog) {
				closeAuthDialog();
			}
		});
		refs.authDialog.addEventListener('keydown', (event) => {
			if (event.key === 'Escape' && !dialogIsNative()) {
				closeAuthDialog();
			}
		});
	}

	// Builds the sections of the schemes the host enables. Without any scheme the button stays hidden.
	function setupAuthorize() {
		refs.authOpen.hidden = app.authSchemes.length === 0;
		for (const scheme of app.authSchemes) {
			const section = buildAuthSection(scheme);
			authSections.set(scheme.id, section);
			refs.authSections.appendChild(section.root);
			updateAuthSection(section);
		}

		if (app.authSchemes.length > 0) {
			wireAuthDialog();
		}

		// The credentials go when the page is left, also when it is only put in the back/forward cache.
		window.addEventListener('pagehide', logoutAll);
	}

	// ------------------------------------------------------------------
	// Operation cards
	// ------------------------------------------------------------------

	function setHint(ctx, message, kind) {
		ctx.hint.textContent = message;
		ctx.hint.className = 'op-hint' + (kind ? ' ' + kind : '');
	}

	function sendBlockReason(state) {
		if (!app.tryItOut) {
			return TEXT_DISABLED;
		}

		return state.version ? '' : TEXT_NO_VERSION;
	}

	function refreshAvailability(ctx) {
		const reason = sendBlockReason(ctx.state);
		ctx.sendButton.disabled = ctx.busy || reason !== '';
		ctx.sendButton.setAttribute('title', reason);
		ctx.sendHint.hidden = reason !== '';
		ctx.cancelButton.hidden = !ctx.busy;
		ctx.resetButton.disabled = ctx.busy;
		if (reason !== '' && !ctx.busy) {
			setHint(ctx, reason, 'warn');
		}
	}

	// Writes the sample envelope into the card. Waits for the WSDL first. An envelope the
	// user has edited is only replaced when force is true. Only the latest call of a card writes (genToken), so a
	// late answer never overwrites a newer sample or an envelope restored from the history.
	async function generateEnvelope(ctx, force) {
		const state = ctx.state;
		ctx.genToken += 1;
		const token = ctx.genToken;
		ctx.envelope.setAttribute('placeholder', 'Generating the sample envelope...');
		await ensureWsdl(state, force);
		if (token !== ctx.genToken) {
			return;
		}

		ctx.envelope.removeAttribute('placeholder');
		if (ctx.edited && !force) {
			return;
		}

		const version = state.version;
		if (!version) {
			ctx.envelope.setAttribute('placeholder', TEXT_NO_VERSION);
			setEnvelopeText(ctx, '');
			return;
		}

		let result = state.wsdl.model ? Wsdl.buildEnvelope(state.wsdl.model, ctx.op, version) : null;
		const detailed = hasOperationDetails(ctx.op);
		let note = '';
		if (!result) {
			note = detailed ? 'Sample built from the operation details because the WSDL is not available.' : '';
		} else if (!result.ok) {
			note = 'No schema based sample: ' + result.error + (detailed ? ' A sample built from the operation details is used.' : ' A generic envelope is used.');
		}

		if (!result || !result.ok) {
			result = Wsdl.fallbackEnvelope(ctx.op, version);
		}

		if (!result.ok) {
			setEnvelopeText(ctx, '');
			setHint(ctx, result.error, 'err');
			return;
		}

		ctx.generated = true;
		ctx.edited = false;
		ctx.generatedVersion = version;
		setEnvelopeText(ctx, result.text);
		const notes = (note + ' ' + (result.warnings || []).join(' ')).trim();
		setHint(ctx, notes, notes === '' ? '' : 'warn');
		refreshAvailability(ctx);
	}

	// ------------------------------------------------------------------
	// Editor helpers: live XML check and Format XML
	// ------------------------------------------------------------------

	function setXmlStatus(ctx, message, kind) {
		if (ctx.xmlStatus.textContent !== message) {
			ctx.xmlStatus.textContent = message;
		}

		ctx.xmlStatus.className = 'op-xml-status' + (kind ? ' ' + kind : '');
		if (kind === 'err') {
			ctx.envelope.setAttribute('aria-invalid', 'true');
		} else {
			ctx.envelope.removeAttribute('aria-invalid');
		}
	}

	// Says whether the editor text is well-formed XML. It only informs: what may be sent is decided by validateEnvelope.
	function updateXmlStatus(ctx) {
		window.clearTimeout(ctx.xmlTimer);
		const value = ctx.envelope.value;
		if (value.trim() === '') {
			setXmlStatus(ctx, '', '');
			return;
		}

		if (value.length > MAX_LIVE_CHECK_CHARS) {
			setXmlStatus(ctx, 'The live XML check is skipped for text over 256 KB.', '');
			return;
		}

		const check = Wsdl.checkXml(value);
		if (check.ok) {
			setXmlStatus(ctx, 'Well-formed XML', 'ok');
		} else {
			setXmlStatus(ctx, 'XML error: ' + check.error + (check.line > 0 ? ' (line ' + check.line + ')' : ''), 'err');
		}
	}

	function scheduleXmlCheck(ctx) {
		window.clearTimeout(ctx.xmlTimer);
		ctx.xmlTimer = window.setTimeout(() => updateXmlStatus(ctx), LIVE_CHECK_DELAY_MS);
	}

	function refreshEditor(ctx) {
		if (ctx.editor) {
			ctx.editor.refresh();
		}
	}

	// Every change of the editor text that code makes (a sample, Format XML, the history) goes through here, so the colours, the
	// live check and the Form view follow it. Typing is followed by the editor itself.
	function setEnvelopeText(ctx, value) {
		ctx.envelope.value = value;
		refreshEditor(ctx);
		updateXmlStatus(ctx);
		refreshViewSwitch(ctx);
	}

	// Pretty prints the editor text. When it cannot be formatted the text stays as it is and the reason goes to the hint.
	function formatEnvelope(ctx) {
		const value = ctx.envelope.value;
		if (value.length > MAX_PRETTY_CHARS) {
			setHint(ctx, 'The envelope has more than 1,000,000 characters and is not formatted.', 'err');
			return;
		}

		let formatted;
		try {
			formatted = Wsdl.prettyPrintXml(value);
		} catch (error) {
			setHint(ctx, 'Format XML failed: ' + (error && error.message ? error.message : 'invalid XML'), 'err');
			return;
		}

		ctx.edited = true;
		setHint(ctx, '', '');
		setEnvelopeText(ctx, formatted);
	}

	// Ctrl+Enter or Cmd+Enter anywhere in the card sends the request, under the same guards as the Send button.
	function onCardKeydown(ctx, event) {
		if (event.key !== 'Enter' || !(event.ctrlKey || event.metaKey) || event.isComposing) {
			return;
		}

		event.preventDefault();
		if (!event.repeat && !ctx.sendButton.disabled) {
			sendOperation(ctx);
		}
	}

	// ------------------------------------------------------------------
	// Request views: the XML editor and the Form
	// ------------------------------------------------------------------

	// The Form view needs form-editor.js, a browser it supports and a host that has not switched it off.
	function formSupported() {
		const form = window.SoapUiForm;
		return app.formEditor && hasWsdlFunction('buildFormTemplate') && !!form && typeof form.mount === 'function'
			&& attempt(() => form.isSupported() === true, false);
	}

	// The SOAP version of the editor text: the one it was made for, otherwise the one that is selected.
	function formVersion(ctx) {
		return ctx.generated && ctx.generatedVersion ? ctx.generatedVersion : ctx.state.version;
	}

	// The form template of the operation, or null when there is none: no schema model yet, no SOAP version, or an operation
	// that the form cannot show. It is built once per model and version.
	function formTemplateFor(ctx) {
		const model = ctx.state.wsdl.model;
		const version = formVersion(ctx);
		if (!model || !version || !formSupported()) {
			return null;
		}

		if (!ctx.formTemplate || ctx.formTemplate.model !== model || ctx.formTemplate.version !== version) {
			ctx.formTemplate = { model: model, version: version, result: attempt(() => Wsdl.buildFormTemplate(model, ctx.op, version), null) };
		}

		const result = ctx.formTemplate.result;
		return result && result.ok === true ? result.template : null;
	}

	// Invalid fields do not block Send: the XML keeps the last valid text.
	function showFormInvalid(ctx, count) {
		ctx.formNote.hidden = count === 0;
		if (count > 0) {
			const subject = count === 1 ? '1 field in the form has an invalid value' : count + ' fields in the form have invalid values';
			ctx.formNote.textContent = subject + '; the XML view still has the last valid text.';
		}
	}

	// The form edits the XML through this: the text goes into the editor as if it was typed, and nothing is added to the history.
	function onFormText(ctx, value) {
		ctx.envelope.value = value;
		ctx.edited = true;
		scheduleXmlCheck(ctx);
	}

	function disposeForm(ctx) {
		if (ctx.form) {
			attempt(() => ctx.form.dispose(), null);
			ctx.form = null;
			ctx.formUsed = null;
			ctx.formHost.replaceChildren();
		}

		showFormInvalid(ctx, 0);
	}

	function mountForm(ctx, template) {
		ctx.form = attempt(() => window.SoapUiForm.mount(ctx.formHost, {
			template: template,
			version: formVersion(ctx),
			readOnly: false,
			getText: () => ctx.envelope.value,
			setText: (value) => onFormText(ctx, value),
			onStateChange: (info) => showFormInvalid(ctx, info && Number.isInteger(info.invalidCount) ? info.invalidCount : 0)
		}), null) || null;
		ctx.formUsed = ctx.form ? template : null;
		return ctx.form !== null;
	}

	// Makes the Form show the current editor text: a form for this template is refreshed, any other is replaced by a new one.
	// Returns false when the form cannot be shown.
	function syncForm(ctx, template) {
		if (ctx.form && ctx.formUsed === template) {
			attempt(() => ctx.form.refresh(), null);
		} else {
			disposeForm(ctx);
			if (!mountForm(ctx, template)) {
				return false;
			}
		}

		showFormInvalid(ctx, attempt(() => ctx.form.invalidCount(), 0));
		return true;
	}

	function setPaneRole(pane, tab, tabs) {
		if (tabs) {
			pane.setAttribute('role', 'tabpanel');
			pane.setAttribute('aria-labelledby', tab.id);
		} else {
			pane.removeAttribute('role');
			pane.removeAttribute('aria-labelledby');
		}
	}

	// Shows the XML editor or the Form. The Form is shown only while the operation has a form template that can be mounted.
	function showView(ctx, view, moveFocus) {
		const template = view === 'form' ? formTemplateFor(ctx) : null;
		const form = template !== null && syncForm(ctx, template);
		if (!form) {
			disposeForm(ctx);
		}

		ctx.view = form ? 'form' : 'xml';
		for (const [tab, selected] of [[ctx.tabForm, form], [ctx.tabXml, !form]]) {
			tab.setAttribute('aria-selected', String(selected));
			tab.setAttribute('tabindex', selected ? '0' : '-1');
		}

		ctx.paneXml.hidden = form;
		ctx.paneForm.hidden = !form;
		if (!form) {
			// The form may have changed the text while the editor was hidden.
			refreshEditor(ctx);
			updateXmlStatus(ctx);
		}

		if (moveFocus) {
			(form ? ctx.tabForm : ctx.tabXml).focus();
		}
	}

	// The tabs exist only while the operation has a form template. When it goes away while the Form is shown, the XML comes back.
	function refreshViewSwitch(ctx) {
		const template = formTemplateFor(ctx);
		const available = template !== null;
		ctx.viewTabs.hidden = !available;
		setPaneRole(ctx.paneXml, ctx.tabXml, available);
		setPaneRole(ctx.paneForm, ctx.tabForm, available);
		if (ctx.view === 'form' && !(available && syncForm(ctx, template))) {
			showView(ctx, 'xml', false);
		}
	}

	function refreshViewSwitches(state) {
		for (const ctx of state.ops) {
			if (ctx.body) {
				refreshViewSwitch(ctx);
			}
		}
	}

	// Frees what a card holds when the card is dropped for good: the coloured editor, the form and the response viewer.
	function disposeOperation(ctx) {
		if (ctx.body) {
			window.clearTimeout(ctx.xmlTimer);
			disposeForm(ctx);
			if (ctx.editor) {
				ctx.editor.dispose();
			}

			ctx.resp.viewer.dispose();
		}
	}

	// ------------------------------------------------------------------
	// Opening an operation and its deep link
	// ------------------------------------------------------------------

	// The body of a card is built the first time the card is opened (see ensureOperationBody).
	function setOperationOpen(ctx, open) {
		if (open) {
			ensureOperationBody(ctx);
		}

		ctx.header.setAttribute('aria-expanded', String(open));
		if (ctx.body) {
			ctx.body.hidden = !open;
		}

		ctx.card.classList.toggle('open', open);
		if (open && !ctx.generated) {
			generateEnvelope(ctx, false);
		}
	}

	// Opening an operation puts its link in the address bar. Closing it takes the link out again, but only when it was this one.
	function toggleOperation(ctx) {
		const open = ctx.header.getAttribute('aria-expanded') !== 'true';
		setOperationOpen(ctx, open);
		if (open) {
			rememberRoute(ctx.state.service, ctx.op.name);
		} else if (Wsdl.parseHashRoute(window.location.hash).operationName === ctx.op.name) {
			rememberRoute(ctx.state.service, '');
		}
	}

	// Opens an operation of a service that is shown, scrolls to it and moves the focus to its header.
	// A name that matches no operation is ignored.
	function revealOperation(state, operationName) {
		const ctx = state.ops.find((candidate) => candidate.op.name === operationName);
		if (!ctx) {
			return;
		}

		if (ctx.card.hidden) {
			refs.operationSearch.value = '';
			applyOperationFilter();
		}

		activateTab(refs.tabOperations, false);
		setOperationOpen(ctx, true);
		rememberRoute(state.service, operationName);
		ctx.card.scrollIntoView({ block: 'start' });
		ctx.header.focus({ preventScroll: true });
	}

	function copyOperationLink(ctx) {
		const link = window.location.href.replace(/#.*$/, '') + Wsdl.buildHashRoute(ctx.state.service.id, ctx.op.name);
		copyText(link, null).then((copied) => {
			if (!copied) {
				// Without the clipboard the link is shown and selected so it can be copied by hand.
				setHint(ctx, link, 'warn');
				selectContents(ctx.hint);
			}

			flash(ctx.linkButton, copied ? 'Link copied' : 'Copy manually');
		});
	}

	// The highlighted panel for a SOAP fault: code, reason and a collapsible detail.
	function buildFaultPanel() {
		const code = el('dd', { class: 'fault-code' });
		const reason = el('dd', { class: 'fault-reason' });
		const codeRow = el('div', { class: 'fault-row' }, [el('dt', null, 'Code'), code]);
		const reasonRow = el('div', { class: 'fault-row' }, [el('dt', null, 'Reason'), reason]);
		const detailText = el('pre', { class: 'code', tabindex: '0' });
		const detailBox = el('details', { class: 'fault-detail' }, [el('summary', null, 'Detail'), detailText]);
		const root = el('div', { class: 'fault-panel', role: 'alert', hidden: true }, [
			el('p', { class: 'fault-label' }, 'SOAP Fault'),
			el('dl', { class: 'fault-fields' }, [codeRow, reasonRow]),
			detailBox
		]);
		return { root, code, codeRow, reason, reasonRow, detailBox, detailText };
	}

	function showFault(panel, fault) {
		panel.root.hidden = fault === null;
		if (fault === null) {
			return;
		}

		panel.code.textContent = fault.code;
		panel.codeRow.hidden = fault.code === '';
		panel.reason.textContent = fault.reason;
		panel.reasonRow.hidden = fault.reason === '';
		panel.detailText.textContent = fault.detail;
		panel.detailBox.hidden = fault.detail === '';
		panel.detailBox.open = false;
	}

	// The response viewer of xml-view.js (colours, folding, search). Without it, or when it fails, the body is shown as plain text.
	function createResponseViewer() {
		const library = window.SoapUiXmlView;
		const viewer = library && typeof library.createViewer === 'function' ? attempt(() => library.createViewer({ label: 'response body' }), null) : null;
		if (viewer) {
			return viewer;
		}

		const pre = el('pre', { class: 'code resp-body', tabindex: '0', role: 'region', 'aria-label': 'Response body', hidden: true });
		return {
			root: pre,
			content: pre,
			setText: (value) => {
				pre.textContent = value;
				pre.hidden = value === '';
			},
			dispose: () => {}
		};
	}

	function buildResponsePanel(ctx) {
		const status = el('span', { class: 'resp-status' });
		const time = el('span', { class: 'resp-time muted' });
		const size = el('span', { class: 'resp-size muted' });
		const type = el('span', { class: 'resp-type muted' });
		const copy = el('button', { class: 'btn', type: 'button' }, 'Copy response');
		const download = el('button', { class: 'btn', type: 'button' }, 'Download response');
		const fault = buildFaultPanel();
		const note = el('p', { class: 'sbar warn resp-note', hidden: true });
		const headersPre = el('pre', { class: 'code', tabindex: '0' });
		const headersBox = el('details', { class: 'resp-headers' }, [el('summary', null, 'Response headers'), headersPre]);
		const viewer = createResponseViewer();
		const root = el('div', { class: 'resp', hidden: true }, [
			el('div', { class: 'resp-meta' }, [status, time, size, type, el('span', { class: 'resp-buttons' }, [copy, download])]),
			fault.root,
			note,
			headersBox,
			viewer.root
		]);
		copy.addEventListener('click', () => {
			copyText(ctx.responseText, viewer.content).then((copied) => {
				flash(copy, copied ? 'Copied' : 'Select and copy manually');
			});
		});
		download.addEventListener('click', () => {
			downloadText(safeFileName(ctx.op.name, 'operation') + '-response.xml', ctx.responseText);
		});
		ctx.resp = { root, status, time, size, type, copy, download, fault, note, headersBox, headersPre, viewer };
		return root;
	}

	// Writes the size and the Content-Type next to the status. Both stay empty when unknown.
	function showResponseFacts(view, bytes, truncated, contentType) {
		view.size.textContent = bytes === null ? '' : Wsdl.formatBytes(bytes) + (truncated ? '+' : '');
		view.type.textContent = contentType;
		view.type.setAttribute('title', contentType);
		view.type.hidden = contentType === '';
	}

	function showPending(ctx) {
		const view = ctx.resp;
		view.root.hidden = false;
		view.status.textContent = 'Sending...';
		view.status.className = 'resp-status pend';
		view.time.textContent = '';
		showResponseFacts(view, null, false, '');
		showFault(view.fault, null);
		view.note.hidden = true;
		view.headersBox.hidden = true;
		view.viewer.setText('');
		view.copy.disabled = true;
		view.download.disabled = true;
		ctx.responseText = '';
	}

	function formatHeaders(headers) {
		const lines = [];
		headers.forEach((value, name) => {
			lines.push(name + ': ' + value);
		});
		return lines.join('\n');
	}

	// What the page keeps of a fetch response: the status line, the Content-Type and the headers as text.
	function summarizeResponse(response) {
		return {
			status: response.status,
			statusText: response.statusText,
			ok: response.ok,
			contentType: response.headers.get('content-type') || '',
			headers: formatHeaders(response.headers)
		};
	}

	function authHintText() {
		return app.authSchemes.length > 0 ? TEXT_AUTH_HINT : TEXT_AUTH_HINT_HEADERS;
	}

	// Shows a response. outcome is { summary, text, bytes, truncated }. extraNotes are shown after the notes of the response itself.
	function renderResponse(ctx, outcome, elapsed, extraNotes) {
		const view = ctx.resp;
		const summary = outcome.summary;
		const body = outcome.text;
		const blank = body.trim() === '';
		const accepted = blank && (summary.status === 202 || (ctx.op.isOneWay && summary.ok));
		const notes = [];
		let shown = body;
		let fault = null;

		if (accepted) {
			shown = '';
		} else if (blank) {
			shown = '';
			notes.push('The response body is empty.');
		} else if (body.length <= MAX_PRETTY_CHARS) {
			if (!Wsdl.hasDoctype(body)) {
				fault = Wsdl.faultFromText(body);
				try {
					shown = Wsdl.prettyPrintXml(body);
				} catch (error) {
					shown = body;
				}
			}
		} else {
			shown = body.slice(0, MAX_PRETTY_CHARS);
			notes.push('The body has more than 1,000,000 characters. The first 1,000,000 are shown. Copy response copies everything that was read.');
		}

		if (outcome.truncated) {
			const limit = Wsdl.formatBytes(app.maxResponseBytes);
			notes.push('The response is larger than ' + limit + '. Only the first ' + limit + ' were read.');
		}

		if ((summary.status === 401 || summary.status === 403) && !isAuthorized()) {
			notes.push(authHintText());
		}

		notes.push(...extraNotes);
		const base = 'HTTP ' + summary.status;
		view.status.textContent = accepted ? base + ' Accepted' : base + (summary.statusText ? ' ' + summary.statusText : '');
		view.status.className = 'resp-status ' + (summary.ok ? 'ok' : 'fail');
		view.time.textContent = elapsed + ' ms';
		showResponseFacts(view, outcome.bytes, outcome.truncated, summary.contentType);
		view.headersPre.textContent = summary.headers;
		view.headersBox.hidden = false;
		view.viewer.setText(shown);
		view.note.textContent = accepted ? NOTE_ACCEPTED : notes.join(' ');
		view.note.className = 'sbar resp-note ' + (accepted ? 'info' : 'warn');
		view.note.hidden = !accepted && notes.length === 0;
		showFault(view.fault, fault);
		ctx.responseText = body;
		view.copy.disabled = body === '';
		view.download.disabled = body === '';
	}

	function failureMessage(error, timeoutSeconds) {
		if (error && error.name === 'AbortError') {
			return 'Cancelled or timed out after ' + timeoutSeconds + ' s.';
		}

		if (error instanceof TypeError) {
			return 'Network error, or the endpoint redirected (redirects are not followed).';
		}

		return 'The request failed: ' + (error && error.message ? error.message : 'unknown error');
	}

	function renderFailure(ctx, message, elapsed) {
		const view = ctx.resp;
		view.status.textContent = 'Request failed';
		view.status.className = 'resp-status fail';
		view.time.textContent = elapsed + ' ms';
		showResponseFacts(view, null, false, '');
		showFault(view.fault, null);
		view.note.hidden = true;
		view.headersBox.hidden = true;
		view.viewer.setText(message, { plain: true });
		view.copy.disabled = true;
		view.download.disabled = true;
		ctx.responseText = '';
	}

	// Shows the outcome of a request: { kind: 'response', summary, text, bytes, truncated } or { kind: 'failure', message }.
	function showOutcome(ctx, outcome, elapsed, extraNotes) {
		ctx.resp.root.hidden = false;
		if (outcome.kind === 'response') {
			renderResponse(ctx, outcome, elapsed, extraNotes);
		} else {
			renderFailure(ctx, outcome.message, elapsed);
		}
	}

	// Validates action, headers and endpoint for a request. Returns { ok: true, ... } or { ok: false, error }.
	function prepareRequest(ctx, requireEnvelope) {
		const state = ctx.state;
		const version = state.version;
		if (!version) {
			return { ok: false, error: TEXT_NO_VERSION };
		}

		const action = ctx.actionInput.value;
		const actionCheck = Wsdl.validateAction(action);
		if (!actionCheck.ok) {
			ctx.actionInput.focus();
			return { ok: false, error: actionCheck.error };
		}

		const custom = readCustomHeaders();
		if (!custom.ok) {
			return { ok: false, error: custom.error };
		}

		const envelope = ctx.envelope.value;
		if (requireEnvelope) {
			const envelopeCheck = Wsdl.validateEnvelope(envelope, version);
			if (!envelopeCheck.ok) {
				ctx.envelope.focus();
				return { ok: false, error: envelopeCheck.error };
			}
		}

		let url;
		try {
			url = sameOriginUrl(state.service.path);
		} catch (error) {
			return { ok: false, error: error.message };
		}

		return { ok: true, version: version, action: action, custom: custom.headers, envelope: envelope, url: url };
	}

	async function sendOperation(ctx) {
		if (ctx.busy) {
			return;
		}

		if (!app.tryItOut) {
			setHint(ctx, TEXT_DISABLED, 'err');
			return;
		}

		setHint(ctx, '', '');
		const request = prepareRequest(ctx, true);
		if (!request.ok) {
			setHint(ctx, request.error, 'err');
			return;
		}

		// WS-Security goes into a copy of the envelope: the editor text is never changed.
		const body = withWsSecurity(request.envelope, request.version, false);
		if (!body.ok) {
			setHint(ctx, body.error, 'err');
			return;
		}

		// Accept first, then Authorize and the custom headers, then the headers the UI owns, so those can never be overridden.
		const headers = new Headers();
		try {
			headers.set('Accept', ACCEPT_SOAP);
			for (const header of withAuthHeaders(request.custom, false)) {
				headers.set(header.name, header.value);
			}

			for (const header of Wsdl.protocolHeaders(request.version, request.action)) {
				headers.set(header.name, header.value);
			}
		} catch (error) {
			setHint(ctx, 'A request header could not be applied: ' + (error && error.message ? error.message : 'invalid value'), 'err');
			return;
		}

		const timeout = readTimeout();
		refreshTimeoutNote();
		ctx.busy = true;
		showPending(ctx);
		refreshAvailability(ctx);
		const controller = new AbortController();
		ctx.controller = controller;
		const timer = window.setTimeout(() => controller.abort(), timeout.seconds * 1000);
		const started = performance.now();
		let outcome;
		try {
			const response = await fetch(request.url, {
				method: 'POST',
				headers: headers,
				body: body.text,
				credentials: 'same-origin',
				cache: 'no-store',
				redirect: 'error',
				referrerPolicy: 'no-referrer',
				signal: controller.signal
			});
			const result = await readTextCapped(response, app.maxResponseBytes);
			outcome = { kind: 'response', summary: summarizeResponse(response), text: result.text, bytes: result.bytes, truncated: result.truncated };
		} catch (error) {
			outcome = { kind: 'failure', message: failureMessage(error, timeout.seconds) };
		} finally {
			window.clearTimeout(timer);
			ctx.controller = null;
			ctx.busy = false;
			refreshAvailability(ctx);
		}

		const elapsed = Math.round(performance.now() - started);
		showOutcome(ctx, outcome, elapsed, []);
		recordHistory(ctx, request, outcome, elapsed);
	}

	// ------------------------------------------------------------------
	// Code snippets
	// ------------------------------------------------------------------

	// The snippet kinds and buildSnippet come from wsdl-parser.js. An older script has only curl, which stays available.
	function snippetsAvailable() {
		return Array.isArray(Wsdl.snippetKinds) && Wsdl.snippetKinds.length > 0 && hasWsdlFunction('buildSnippet');
	}

	function currentSnippetKind() {
		return snippetsAvailable() ? app.snippetKind : 'curl';
	}

	function snippetMessage(kind, copied) {
		if (kind === 'curl') {
			return copied ? TEXT_CURL_COPIED : TEXT_CURL_MANUAL;
		}

		return copied ? TEXT_CODE_COPIED : TEXT_CODE_MANUAL;
	}

	// Authorize goes through the same pipeline as a send, with every secret masked: the header values (also the ones of a
	// custom API key name) and the WS-Security password. The rest of the body is not redacted.
	function copySnippet(ctx) {
		setHint(ctx, '', '');
		const request = prepareRequest(ctx, false);
		if (!request.ok) {
			setHint(ctx, request.error, 'err');
			return;
		}

		const body = withWsSecurity(request.envelope, request.version, true);
		if (!body.ok) {
			setHint(ctx, body.error, 'err');
			return;
		}

		const kind = currentSnippetKind();
		const input = {
			url: request.url,
			headers: Wsdl.protocolHeaders(request.version, request.action).concat(withAuthHeaders(request.custom, true)),
			body: body.text
		};
		const built = snippetsAvailable() ? Wsdl.buildSnippet(kind, input) : Wsdl.curl.build(input);
		if (!built.ok) {
			setHint(ctx, built.error, 'err');
			return;
		}

		ctx.codeOutput.textContent = built.text;
		ctx.codeOutput.hidden = false;
		copyText(built.text, ctx.codeOutput).then((copied) => {
			setHint(ctx, snippetMessage(kind, copied), copied ? 'ok' : 'warn');
		});
	}

	// The kind chosen in one card is the kind of every card, for this page session only.
	function onSnippetKindChanged(kind) {
		app.snippetKind = kind;
		forEachBuiltOperation((ctx) => {
			ctx.snippetSelect.value = kind;
		});
	}

	// ------------------------------------------------------------------
	// Request history (memory only)
	// ------------------------------------------------------------------

	// Keeps the last requests, newest first. The stored envelope is the editor text, never the copy with WS-Security,
	// and no header or credential is stored. A stored response text is cut to 64 KB.
	function recordHistory(ctx, request, outcome, elapsed) {
		const stored = outcome.kind === 'response'
			? Object.assign({}, outcome, { text: outcome.text.slice(0, HISTORY_MAX_TEXT), cut: outcome.text.length > HISTORY_MAX_TEXT })
			: outcome;
		app.history.unshift({
			service: ctx.state.service,
			operationName: ctx.op.name,
			version: request.version,
			action: request.action,
			envelope: request.envelope,
			time: new Date(),
			elapsed: elapsed,
			outcome: stored
		});
		if (app.history.length > HISTORY_LIMIT) {
			app.history.length = HISTORY_LIMIT;
		}

		renderHistory();
	}

	function buildHistoryItem(entry) {
		const outcome = entry.outcome;
		const response = outcome.kind === 'response';
		const time = entry.time.toLocaleTimeString();
		const label = entry.service.name + ' / ' + entry.operationName;
		// After a reload of the catalog a service can be gone. Its entries stay in the list but cannot be loaded.
		const gone = app.services.indexOf(entry.service) === -1;
		const load = el('button', {
			class: 'btn btn-small',
			type: 'button',
			disabled: gone,
			title: gone ? 'This service is no longer in the catalog.' : false,
			'aria-label': 'Load ' + label + ' sent at ' + time
		}, 'Load');
		load.addEventListener('click', () => loadHistoryEntry(entry));
		return el('li', { class: 'history-item' }, [
			el('span', { class: 'history-time muted' }, time),
			el('span', { class: 'history-name' }, label),
			el('span', { class: 'history-facts' }, [
				el('span', { class: 'badge' }, 'SOAP ' + entry.version),
				el('span', { class: 'badge history-status ' + (response && outcome.summary.ok ? 'ok' : 'fail') }, response ? 'HTTP ' + outcome.summary.status : 'Failed'),
				el('span', { class: 'muted' }, entry.elapsed + ' ms'),
				response ? el('span', { class: 'muted' }, Wsdl.formatBytes(outcome.bytes)) : null
			]),
			load
		]);
	}

	function renderHistory() {
		refs.historyList.replaceChildren(...app.history.map(buildHistoryItem));
		refs.historyEmpty.hidden = app.history.length > 0;
		refs.historyClear.disabled = app.history.length === 0;
		refs.historyToggle.textContent = app.history.length === 0 ? 'History' : 'History (' + app.history.length + ')';
	}

	function toggleHistory() {
		const open = refs.historyPanel.hidden;
		refs.historyPanel.hidden = !open;
		refs.historyToggle.setAttribute('aria-expanded', String(open));
	}

	function clearHistory() {
		app.history.length = 0;
		renderHistory();
		refs.historyToggle.focus();
	}

	// Puts the request of an entry back into the card: envelope, SOAPAction and SOAP version, never headers or credentials.
	// The response is shown again from the stored text, so a fault is highlighted again.
	function restoreRequest(ctx, entry) {
		ctx.genToken += 1;
		ctx.envelope.removeAttribute('placeholder');
		ctx.actionInput.value = entry.action;
		ctx.generated = true;
		ctx.generatedVersion = entry.version;
		ctx.edited = true;
		setEnvelopeText(ctx, entry.envelope);
		const notes = ['Restored from the history.'];
		if (entry.outcome.cut) {
			notes.push('Only the first 64 KB of the response were kept.');
		}

		showOutcome(ctx, entry.outcome, entry.elapsed, notes);
		setHint(ctx, 'Loaded from the history. Request headers and credentials are not restored.', '');
	}

	function loadHistoryEntry(entry) {
		if (!app.selected || app.selected.service !== entry.service) {
			selectService(entry.service, false);
		}

		const state = app.selected;
		if (state.version !== entry.version && state.service.soapVersions.indexOf(entry.version) !== -1) {
			state.version = entry.version;
			syncVersionSwitch(state);
			onVersionChanged(state);
		}

		revealOperation(state, entry.operationName);
		const ctx = state.ops.find((candidate) => candidate.op.name === entry.operationName);
		if (!ctx || !ctx.body) {
			return;
		}

		if (ctx.busy) {
			setHint(ctx, 'A request of this operation is still running. Load the entry when it has finished.', 'warn');
			return;
		}

		restoreRequest(ctx, entry);
	}

	function field(label, control, forId) {
		return el('div', { class: 'field' }, [
			forId ? el('label', { class: 'lbl', for: forId }, label) : el('span', { class: 'lbl' }, label),
			control
		]);
	}

	function plural(count, word) {
		return count + ' ' + word + (count === 1 ? '' : 's');
	}

	// The text of a description on one line, for the summary in a card header.
	function oneLine(value) {
		return value.replace(/\s+/g, ' ').trim();
	}

	// The header is one button, so it holds text only: the name, the badges, the shortened SOAPAction and a one line summary.
	// aria-controls is set when the body is built, the first time the card is opened.
	function buildOperationHeader(ctx) {
		const operation = ctx.op;
		const badges = [];
		if (operation.isOneWay) {
			badges.push('one-way');
		}

		if (app.operationDetails && operation.faults && operation.faults.length > 0) {
			badges.push(plural(operation.faults.length, 'fault'));
		}

		if (app.operationDetails && operation.parameters) {
			badges.push(plural(operation.parameters.length, 'param'));
		}

		const action = Wsdl.shortenSoapAction(operation.soapAction, operation.contractNamespace);
		const summary = oneLine(operation.description);
		return el('button', {
			class: 'op-header',
			type: 'button',
			id: 'op-' + ctx.id,
			'aria-expanded': 'false'
		}, [
			el('span', { class: 'op-name' }, operation.name),
			badges.map((label) => el('span', { class: 'badge' }, label)),
			action === '' ? null : el('span', { class: 'op-action', title: operation.soapAction }, action),
			summary === '' ? null : el('span', { class: 'op-summary', title: operation.description }, summary)
		]);
	}

	// The description column exists only when at least one parameter has a description.
	function buildParametersTable(parameters) {
		if (parameters.length === 0) {
			return el('p', { class: 'muted' }, 'No parameters.');
		}

		const described = parameters.some((parameter) => parameter.description !== '');
		const rows = parameters.map((parameter) => el('tr', null, [
			el('th', { scope: 'row', class: 'op-param-name mono' }, parameter.name),
			el('td', { class: 'op-type mono', title: parameter.type }, parameter.type),
			el('td', { class: 'op-param-dir' }, el('span', { class: 'badge ' + (DIRECTION_CLASSES.get(parameter.direction) || '') }, parameter.direction)),
			described ? el('td', { class: 'op-param-desc' }, parameter.description) : null
		]));
		const columns = described ? ['Name', 'Type', 'Direction', 'Description'] : ['Name', 'Type', 'Direction'];
		return el('div', { class: 'table-wrap' }, el('table', { class: 'op-params' }, [
			el('caption', null, 'Parameters'),
			el('thead', null, el('tr', null, columns.map((label) => el('th', { scope: 'col' }, label)))),
			el('tbody', null, rows)
		]));
	}

	function fact(label, value) {
		return el('div', { class: 'op-fact' }, [el('dt', null, label), el('dd', null, value)]);
	}

	// The return type and the declared faults, as a definition list. Null when there is nothing to list.
	function buildOperationFacts(operation) {
		const facts = [];
		if (operation.returns && operation.returns.type !== '') {
			const type = operation.returns.type;
			facts.push(fact('Returns', el('code', { class: 'op-type mono', title: type }, type)));
		}

		if (operation.faults && operation.faults.length > 0) {
			const faults = operation.faults.map((name) => el('li', { class: 'op-type mono', title: name }, name));
			facts.push(fact('Faults', el('ul', { class: 'op-faults' }, faults)));
		}

		return facts.length === 0 ? null : el('dl', { class: 'op-facts' }, facts);
	}

	function buildMessageContractBadge(contract) {
		if (!contract || (!contract.request && !contract.response)) {
			return null;
		}

		const sides = contract.request && contract.response ? 'request and response' : (contract.request ? 'request' : 'response');
		return el('span', { class: 'badge', title: 'Message contract on the ' + sides + '.' }, 'Message contract');
	}

	// The documentation block of an operation. It exists only when the host exposes the details and the operation has some.
	function buildOperationDetails(operation) {
		if (!app.operationDetails) {
			return null;
		}

		const parts = [
			operation.parameters ? buildParametersTable(operation.parameters) : null,
			buildOperationFacts(operation),
			buildMessageContractBadge(operation.messageContract)
		];
		return parts.every((part) => part === null) ? null : el('div', { class: 'op-details', role: 'group', 'aria-label': 'Operation details' }, parts);
	}

	function createOperationControls(ctx) {
		const id = ctx.id;
		ctx.actionInput = el('input', {
			class: 'input op-action-input mono',
			type: 'text',
			id: 'op-action-' + id,
			autocomplete: 'off',
			spellcheck: 'false',
			value: ctx.op.soapAction
		});
		ctx.copyActionButton = el('button', { class: 'btn btn-small', type: 'button' }, 'Copy SOAPAction');
		ctx.envelope = el('textarea', {
			class: 'input op-envelope mono',
			id: 'op-envelope-' + id,
			rows: '14',
			spellcheck: 'false',
			autocomplete: 'off',
			'aria-label': 'Request envelope',
			'aria-describedby': 'op-xml-status-' + id
		});
		ctx.xmlStatus = el('p', { class: 'op-xml-status', id: 'op-xml-status-' + id, role: 'status', 'aria-live': 'polite' });
		createViewControls(ctx);
		ctx.sendButton = el('button', { class: 'btn btn-go op-send', type: 'button', 'aria-keyshortcuts': 'Control+Enter Meta+Enter' }, 'Send');
		ctx.sendHint = el('span', { class: 'op-kbd-hint', title: 'Ctrl+Enter, or Cmd+Enter on macOS' }, 'Ctrl+Enter to send');
		ctx.cancelButton = el('button', { class: 'btn op-cancel', type: 'button', hidden: true }, 'Cancel');
		ctx.resetButton = el('button', { class: 'btn op-reset', type: 'button' }, 'Reset sample');
		ctx.formatButton = el('button', { class: 'btn op-format', type: 'button' }, 'Format XML');
		createSnippetControls(ctx);
		ctx.linkButton = el('button', { class: 'btn btn-small', type: 'button' }, 'Copy link');
		ctx.hint = el('span', { class: 'op-hint', role: 'status' });
		ctx.authNote = el('p', { class: 'op-auth-note muted', hidden: true });
		ctx.codeOutput = el('pre', { class: 'code op-code', tabindex: '0', hidden: true, role: 'region', 'aria-label': 'Generated code' });
	}

	// The coloured editor of xml-view.js. Without it, or when it fails, the textarea is the editor.
	function attachEditor(textarea) {
		const library = window.SoapUiXmlView;
		return library && typeof library.attachEditor === 'function' ? attempt(() => library.attachEditor(textarea), null) : null;
	}

	// The tabs, the two panes and the note of the Form | XML switch. The tabs stay hidden until the Form view is possible.
	function createViewControls(ctx) {
		const id = ctx.id;
		const tab = (name, label, selected) => el('button', {
			class: 'subtab',
			role: 'tab',
			type: 'button',
			id: 'op-view-' + name + '-' + id,
			'aria-selected': String(selected),
			'aria-controls': 'op-pane-' + name + '-' + id,
			tabindex: selected ? false : '-1'
		}, label);
		ctx.tabForm = tab('form', 'Form', false);
		ctx.tabXml = tab('xml', 'XML', true);
		ctx.viewTabs = el('div', { class: 'subtabs', role: 'tablist', 'aria-label': 'Request view', hidden: true }, [ctx.tabForm, ctx.tabXml]);
		ctx.editor = attachEditor(ctx.envelope);
		ctx.paneXml = el('div', { class: 'op-pane', id: 'op-pane-xml-' + id }, ctx.editor ? ctx.editor.root : ctx.envelope);
		ctx.formHost = el('div');
		ctx.paneForm = el('div', { class: 'op-pane op-pane-form', id: 'op-pane-form-' + id, hidden: true }, ctx.formHost);
		ctx.formNote = el('p', { class: 'op-form-note', role: 'status', 'aria-live': 'polite', hidden: true });
	}

	// The snippet selector (kinds from wsdl-parser.js) with its button. Without them there is only the curl button.
	function createSnippetControls(ctx) {
		if (!snippetsAvailable()) {
			ctx.snippetSelect = null;
			ctx.snippetButton = el('button', { class: 'btn op-copy-code', type: 'button' }, 'Copy as curl (bash)');
			return;
		}

		ctx.snippetSelect = el('select', { class: 'input input-small', id: 'op-snippet-' + ctx.id }, Wsdl.snippetKinds.map((kind) => el('option', { value: kind.id }, kind.label)));
		ctx.snippetSelect.value = app.snippetKind;
		ctx.snippetButton = el('button', { class: 'btn op-copy-code', type: 'button' }, 'Copy code');
	}

	function buildSnippetGroup(ctx) {
		return el('span', { class: 'op-code-group' }, [
			ctx.snippetSelect ? el('label', { class: 'op-code-label', for: ctx.snippetSelect.id }, 'Code') : null,
			ctx.snippetSelect,
			ctx.snippetButton
		]);
	}

	function buildOperationBody(ctx) {
		const operation = ctx.op;
		const id = ctx.id;
		return el('div', {
			class: 'op-body',
			id: 'op-body-' + id,
			role: 'region',
			'aria-labelledby': 'op-' + id,
			hidden: true
		}, [
			operation.description === '' ? null : el('p', { class: 'op-description' }, operation.description),
			el('div', { class: 'op-tools', hidden: ctx.state.service.id === '' }, ctx.linkButton),
			field('Endpoint', el('div', { class: 'readonly mono' }, 'POST ' + ctx.state.service.path)),
			operation.contractNamespace ? field('Contract namespace', el('div', { class: 'readonly mono' }, operation.contractNamespace)) : null,
			field('SOAPAction', el('div', { class: 'input-row' }, [ctx.actionInput, ctx.copyActionButton]), 'op-action-' + id),
			buildOperationDetails(operation),
			field('Request envelope', [ctx.viewTabs, ctx.paneXml, ctx.paneForm, ctx.xmlStatus, ctx.formNote], 'op-envelope-' + id),
			el('div', { class: 'op-actions' }, [
				el('span', { class: 'op-send-group' }, [ctx.sendButton, ctx.cancelButton, ctx.sendHint]),
				ctx.resetButton,
				ctx.formatButton,
				buildSnippetGroup(ctx),
				ctx.hint
			]),
			ctx.authNote,
			ctx.codeOutput,
			buildResponsePanel(ctx)
		]);
	}

	function wireOperationBody(ctx) {
		ctx.body.addEventListener('keydown', (event) => onCardKeydown(ctx, event));
		ctx.envelope.addEventListener('input', () => {
			ctx.edited = true;
			scheduleXmlCheck(ctx);
		});
		wireTablist([ctx.tabForm, ctx.tabXml], (tab, moveFocus) => showView(ctx, tab === ctx.tabForm ? 'form' : 'xml', moveFocus));
		ctx.sendButton.addEventListener('click', () => sendOperation(ctx));
		ctx.cancelButton.addEventListener('click', () => {
			if (ctx.controller) {
				ctx.controller.abort();
			}
		});
		ctx.resetButton.addEventListener('click', () => {
			ctx.edited = false;
			ctx.actionInput.value = ctx.op.soapAction;
			generateEnvelope(ctx, true);
		});
		ctx.formatButton.addEventListener('click', () => formatEnvelope(ctx));
		ctx.snippetButton.addEventListener('click', () => copySnippet(ctx));
		if (ctx.snippetSelect) {
			ctx.snippetSelect.addEventListener('change', () => onSnippetKindChanged(ctx.snippetSelect.value));
		}

		ctx.linkButton.addEventListener('click', () => copyOperationLink(ctx));
		ctx.copyActionButton.addEventListener('click', () => {
			copyText(ctx.actionInput.value, ctx.actionInput).then((copied) => {
				flash(ctx.copyActionButton, copied ? 'Copied' : 'Select and copy manually');
			});
		});
	}

	function buildOperationCard(state, operation) {
		const id = app.nextOpId;
		app.nextOpId += 1;
		const ctx = {
			state: state,
			op: operation,
			id: id,
			haystack: (operation.name + ' ' + operation.soapAction).toLowerCase(),
			generated: false,
			generatedVersion: null,
			edited: false,
			busy: false,
			controller: null,
			xmlTimer: 0,
			genToken: 0,
			responseText: '',
			view: 'xml',
			editor: null,
			form: null,
			formUsed: null,
			formTemplate: null,
			body: null
		};

		// Only the header exists up front. The body (editor, buttons, response panel, tables) is built when the card is first opened.
		ctx.header = buildOperationHeader(ctx);
		ctx.card = el('article', { class: 'op-card' }, el('h3', { class: 'op-title' }, ctx.header));
		ctx.header.addEventListener('click', () => toggleOperation(ctx));
		return ctx;
	}

	function ensureOperationBody(ctx) {
		if (ctx.body) {
			return;
		}

		createOperationControls(ctx);
		ctx.body = buildOperationBody(ctx);
		ctx.header.setAttribute('aria-controls', 'op-body-' + ctx.id);
		ctx.card.appendChild(ctx.body);
		wireOperationBody(ctx);
		refreshAvailability(ctx);
		refreshAuthNote(ctx);
		refreshViewSwitch(ctx);
	}

	function buildServiceContainer(state) {
		const service = state.service;
		const container = el('div', { class: 'op-service' });
		state.warnBar = el('p', { class: 'sbar warn', role: 'status', hidden: true });
		state.noMatch = el('p', { class: 'muted', hidden: true }, 'No operations match your filter.');
		container.appendChild(state.warnBar);
		if (service.operations.length === 0 && service.operationsError === '') {
			container.appendChild(el('p', { class: 'muted' }, 'This service exposes no operations.'));
		}

		state.ops = service.operations.map((operation) => {
			const ctx = buildOperationCard(state, operation);
			container.appendChild(ctx.card);
			return ctx;
		});
		container.appendChild(state.noMatch);
		state.container = container;
	}

	function onVersionChanged(state) {
		for (const ctx of state.ops) {
			if (!ctx.body) {
				continue;
			}

			refreshAvailability(ctx);
			if (!ctx.generated) {
				continue;
			}

			if (ctx.edited) {
				setHint(ctx, 'The envelope was edited and still uses the SOAP ' + ctx.generatedVersion + ' namespace. Update it, or press Reset sample.', 'warn');
			} else {
				generateEnvelope(ctx, true);
			}
		}
	}

	function applyOperationFilter() {
		const state = app.selected;
		if (!state) {
			return;
		}

		const query = refs.operationSearch.value.trim().toLowerCase();
		let visible = 0;
		for (const ctx of state.ops) {
			const match = query === '' || ctx.haystack.indexOf(query) !== -1;
			ctx.card.hidden = !match;
			if (match) {
				visible += 1;
			}
		}

		state.noMatch.hidden = !(state.ops.length > 0 && visible === 0);
	}

	// ------------------------------------------------------------------
	// Service list and selection
	// ------------------------------------------------------------------

	// A short badge for the service list. The full text is the accessible name and the tooltip.
	function listBadge(label, description) {
		return el('span', { class: 'badge', role: 'img', title: description, 'aria-label': description }, label);
	}

	function soapVersionBadge(service) {
		const label = service.soapVersions.join(' / ');
		return label === '' ? null : listBadge(label, 'SOAP ' + label);
	}

	// The serializer in short form: DCS, XML, Custom, or the text as it is for a serializer this page does not know.
	function serializerBadge(service) {
		if (service.customSerializerType) {
			return listBadge('Custom', 'Custom serializer: ' + service.customSerializerType);
		}

		switch (service.serializer) {
			case '':
				return null;
			case 'DataContractSerializer':
				return listBadge('DCS', 'Serializer: DataContractSerializer');
			case 'XmlSerializer':
				return listBadge('XML', 'Serializer: XmlSerializer');
			default:
				return listBadge(service.serializer, 'Serializer: ' + service.serializer);
		}
	}

	// The unqualified name of a service type: Ns.IService gives IService. Generic arguments and an assembly part are dropped.
	function shortTypeName(type) {
		const bare = type.split(/[<`[]/)[0];
		const name = bare.slice(bare.lastIndexOf('.') + 1);
		return name === '' ? type : name;
	}

	// Services that share a service type (one contract on more than one endpoint) form a group. All others stay single entries.
	// Returns slots in list order: { type, services }, with type empty for a single entry.
	function groupByContract(services) {
		const counts = new Map();
		for (const service of services) {
			counts.set(service.serviceType, (counts.get(service.serviceType) || 0) + 1);
		}

		const slots = [];
		const groups = new Map();
		for (const service of services) {
			if (service.serviceType === '' || counts.get(service.serviceType) < 2) {
				slots.push({ type: '', services: [service] });
				continue;
			}

			let slot = groups.get(service.serviceType);
			if (!slot) {
				slot = { type: service.serviceType, services: [] };
				groups.set(service.serviceType, slot);
				slots.push(slot);
			}

			slot.services.push(service);
		}

		return slots;
	}

	// One button of the list. In a group the path is the name, because the endpoints of a contract differ by path.
	function buildServiceEntry(service, grouped) {
		// Shown only after the WSDL request of this service failed with 403 or 404.
		const wsdlBadge = el('span', {
			class: 'badge badge-warn',
			title: 'The WSDL request was refused or the URL was not found (HTTP 403 or 404).',
			hidden: true
		}, 'WSDL unavailable');
		const head = el('span', { class: 'service-item-head' }, [
			el('span', { class: 'service-item-name' + (grouped ? ' service-item-route' : '') }, grouped ? service.path : service.name),
			el('span', { class: 'service-item-badges' }, [soapVersionBadge(service), serializerBadge(service), wsdlBadge])
		]);
		const button = el('button', { class: 'service-item', type: 'button', 'data-service-id': service.id }, [
			head,
			grouped ? null : el('span', { class: 'service-item-path' }, service.path)
		]);
		button.addEventListener('click', () => selectService(service, true));
		return {
			service: service,
			item: el('li', null, button),
			button: button,
			wsdlBadge: wsdlBadge,
			haystack: (service.name + ' ' + service.path + ' ' + service.serviceType).toLowerCase()
		};
	}

	// A group is a list item with the contract name and a nested list that has its own label.
	function appendServiceGroup(slot) {
		const entries = slot.services.map((service) => buildServiceEntry(service, true));
		const title = shortTypeName(slot.type);
		const item = el('li', { class: 'service-group' }, [
			el('p', { class: 'service-group-title', title: slot.type }, [title, el('span', { class: 'muted' }, plural(entries.length, 'endpoint'))]),
			el('ul', { class: 'service-sublist', 'aria-label': title + ' endpoints' }, entries.map((entry) => entry.item))
		]);
		app.groups.push({ item: item, entries: entries });
		app.entries.push(...entries);
		refs.serviceList.appendChild(item);
	}

	function buildServiceList() {
		refs.serviceList.replaceChildren();
		app.entries = [];
		app.groups = [];
		for (const slot of groupByContract(app.services)) {
			if (slot.type === '') {
				const entry = buildServiceEntry(slot.services[0], false);
				app.entries.push(entry);
				refs.serviceList.appendChild(entry.item);
			} else {
				appendServiceGroup(slot);
			}
		}
	}

	// Filters by name, path and type. A group with no matching endpoint is hidden.
	function applyServiceFilter() {
		const query = refs.serviceSearch.value.trim().toLowerCase();
		let visible = 0;
		for (const entry of app.entries) {
			const match = query === '' || entry.haystack.indexOf(query) !== -1;
			entry.item.hidden = !match;
			if (match) {
				visible += 1;
			}
		}

		for (const group of app.groups) {
			group.item.hidden = group.entries.every((entry) => entry.item.hidden);
		}

		refs.serviceListEmpty.hidden = visible > 0 || app.entries.length === 0;
	}

	// Puts the deep link of a service, or of one of its operations, in the address bar without adding a history entry.
	function rememberRoute(service, operationName) {
		if (service.id === '') {
			return;
		}

		try {
			window.history.replaceState(null, '', Wsdl.buildHashRoute(service.id, operationName));
		} catch (error) {
			// The address bar cannot be updated in some embedded contexts. Selection still works.
		}
	}

	// What the deep link in the address bar points to. The service is null when the link names none that exists.
	function routeFromHash() {
		const route = Wsdl.parseHashRoute(window.location.hash);
		return {
			service: route.serviceId === '' ? null : (app.byId.get(route.serviceId) || null),
			operationName: route.operationName
		};
	}

	function absoluteWsdlUrl(service) {
		if (service.wsdlUrl === '') {
			return '';
		}

		try {
			return sameOriginUrl(service.wsdlUrl);
		} catch (error) {
			return '';
		}
	}

	// Everything that depends on the WSDL URL: the client command, "Copy WSDL URL" and "Open raw WSDL".
	function renderWsdlTools(service) {
		const url = absoluteWsdlUrl(service);
		const command = url === '' ? null : Wsdl.buildSvcutilCommand(url);
		refs.serviceClient.hidden = !(command && command.ok);
		refs.serviceClientCommand.textContent = command && command.ok ? command.text : '';
		refs.wsdlCopyUrl.disabled = url === '';
		refs.wsdlOpen.hidden = url === '';
		if (url === '') {
			refs.wsdlOpen.removeAttribute('href');
		} else {
			refs.wsdlOpen.href = url;
		}
	}

	// Shows the SOAP version radios of a service: only an endpoint that exposes both versions offers the choice.
	function syncVersionSwitch(state) {
		const versions = state.service.soapVersions;
		refs.soapVersion.hidden = !(versions.indexOf('1.1') !== -1 && versions.indexOf('1.2') !== -1);
		refs.soapVersion11.checked = state.version === '1.1';
		refs.soapVersion12.checked = state.version === '1.2';
	}

	function renderServiceHeader(state) {
		const service = state.service;
		refs.serviceName.textContent = service.name;
		refs.serviceMeta.textContent = service.path + (service.serviceType ? ' | ' + service.serviceType : '');
		refs.serviceDescription.textContent = service.description;
		refs.serviceDescription.hidden = service.description === '';

		refs.serviceBadges.replaceChildren();
		const badges = [];
		if (service.soapVersions.length > 0) {
			badges.push('SOAP ' + service.soapVersions.join(' / '));
		} else {
			badges.push('No SOAP envelope');
		}

		if (service.serializer) {
			badges.push(service.serializer);
		}

		if (service.customSerializerType) {
			badges.push('Custom: ' + service.customSerializerType);
		}

		if (service.caseInsensitivePath) {
			badges.push('Case-insensitive path');
		}

		badges.push(service.operations.length + (service.operations.length === 1 ? ' operation' : ' operations'));
		for (const label of badges) {
			refs.serviceBadges.appendChild(el('span', { class: 'badge' }, label));
		}

		syncVersionSwitch(state);
		refs.operationsError.textContent = service.operationsError;
		refs.operationsError.hidden = service.operationsError === '';
		renderWsdlTools(service);
	}

	function markCurrentEntry(service) {
		for (const entry of app.entries) {
			if (entry.service === service) {
				entry.button.setAttribute('aria-current', 'true');
			} else {
				entry.button.removeAttribute('aria-current');
			}
		}
	}

	function selectService(service, userInitiated) {
		const state = stateFor(service);
		app.selected = state;
		markCurrentEntry(service);
		rememberRoute(service, '');
		renderServiceHeader(state);
		const firstTime = !state.container;
		if (firstTime) {
			buildServiceContainer(state);
		}

		refs.operationsList.replaceChildren(state.container);
		refs.operationSearch.value = '';
		applyOperationFilter();
		refs.serviceView.hidden = false;
		activateTab(refs.tabOperations, false);
		refreshWsdlPanel(state);
		ensureWsdl(state);
		if (firstTime && app.docExpansion === 'full') {
			setAllOperationsOpen(true);
		}

		if (userInitiated) {
			if (isSmallScreen()) {
				setSidebarOpen(false);
			}

			refs.serviceName.focus();
		}
	}

	// Opens or closes every operation that the filter shows. The bodies are built when a card opens, so a large service
	// pays for what is opened only.
	function setAllOperationsOpen(open) {
		const state = app.selected;
		if (!state) {
			return;
		}

		const linked = Wsdl.parseHashRoute(window.location.hash).operationName;
		let closedLinked = false;
		for (const ctx of state.ops) {
			if (!ctx.card.hidden) {
				setOperationOpen(ctx, open);
				closedLinked = closedLinked || (!open && ctx.op.name === linked);
			}
		}

		if (closedLinked) {
			rememberRoute(state.service, '');
		}
	}

	// ------------------------------------------------------------------
	// Sidebar on small screens, theme and host stylesheet
	// ------------------------------------------------------------------

	function isSmallScreen() {
		return window.matchMedia(SMALL_SCREEN_QUERY).matches;
	}

	// Below 800 px the sidebar is a panel that the Services button shows and hides. On wide screens it is always shown.
	function setSidebarOpen(open) {
		refs.sidebar.classList.toggle('is-collapsed', !open);
		refs.sidebarToggle.setAttribute('aria-expanded', String(open));
	}

	function savedTheme() {
		try {
			const saved = window.localStorage.getItem(THEME_KEY);
			return THEMES.indexOf(saved) === -1 ? 'system' : saved;
		} catch (error) {
			return 'system';
		}
	}

	// Only the preference is stored. When the storage is blocked the choice still applies until the page is left.
	function saveTheme(theme) {
		try {
			if (theme === 'system') {
				window.localStorage.removeItem(THEME_KEY);
			} else {
				window.localStorage.setItem(THEME_KEY, theme);
			}
		} catch (error) {
			// Not stored: the theme is chosen again next time.
		}
	}

	// System follows prefers-color-scheme (no attribute). Light and dark set data-theme, which style.css reads.
	function applyTheme(theme) {
		if (theme === 'light' || theme === 'dark') {
			document.documentElement.setAttribute('data-theme', theme);
		} else {
			document.documentElement.removeAttribute('data-theme');
		}
	}

	// StylesheetUrl of the host: a path on this origin that starts with exactly one slash. Anything else is ignored silently.
	function applyHostStylesheet(value) {
		if (!/^\/(?!\/)[^\s\\\u0000-\u001F]*$/.test(value)) {
			return;
		}

		let url;
		try {
			url = sameOriginUrl(value);
		} catch (error) {
			return;
		}

		const link = document.createElement('link');
		link.rel = 'stylesheet';
		link.href = url;
		document.head.appendChild(link);
	}

	// ------------------------------------------------------------------
	// Tabs
	// ------------------------------------------------------------------

	function activateTab(tab, moveFocus) {
		for (const candidate of [refs.tabOperations, refs.tabWsdl]) {
			const selected = candidate === tab;
			candidate.setAttribute('aria-selected', String(selected));
			candidate.setAttribute('tabindex', selected ? '0' : '-1');
			document.getElementById(candidate.getAttribute('aria-controls')).hidden = !selected;
		}

		if (tab === refs.tabWsdl && app.selected) {
			refreshWsdlViews(app.selected);
		}

		if (moveFocus) {
			tab.focus();
		}
	}

	// Clicks and the arrow, Home and End keys of a row of tabs. activate(tab, moveFocus) shows the tab.
	function wireTablist(tabs, activate) {
		tabs.forEach((tab, index) => {
			tab.addEventListener('click', () => activate(tab, false));
			tab.addEventListener('keydown', (event) => {
				let target = null;
				if (event.key === 'ArrowRight') {
					target = tabs[(index + 1) % tabs.length];
				} else if (event.key === 'ArrowLeft') {
					target = tabs[(index + tabs.length - 1) % tabs.length];
				} else if (event.key === 'Home') {
					target = tabs[0];
				} else if (event.key === 'End') {
					target = tabs[tabs.length - 1];
				}

				if (target) {
					event.preventDefault();
					activate(target, true);
				}
			});
		});
	}

	// ------------------------------------------------------------------
	// Startup
	// ------------------------------------------------------------------

	function wireStaticEvents() {
		refs.serviceSearch.addEventListener('input', applyServiceFilter);
		refs.operationSearch.addEventListener('input', applyOperationFilter);
		refs.headersAdd.addEventListener('click', () => {
			addHeaderRow().focus();
		});
		refs.operationsExpand.addEventListener('click', () => setAllOperationsOpen(true));
		refs.operationsCollapse.addEventListener('click', () => setAllOperationsOpen(false));
		refs.sidebarToggle.addEventListener('click', () => {
			setSidebarOpen(refs.sidebarToggle.getAttribute('aria-expanded') !== 'true');
		});
		refs.historyToggle.addEventListener('click', toggleHistory);
		refs.historyClear.addEventListener('click', clearHistory);
		refs.themeSelect.addEventListener('change', () => {
			applyTheme(refs.themeSelect.value);
			saveTheme(refs.themeSelect.value);
		});
		const onVersion = (event) => {
			const state = app.selected;
			if (!state || !event.target.checked) {
				return;
			}

			state.version = event.target.value === '1.2' ? '1.2' : '1.1';
			onVersionChanged(state);
		};
		refs.soapVersion11.addEventListener('change', onVersion);
		refs.soapVersion12.addEventListener('change', onVersion);
		// The address bar is only read here when the user navigates. Updating it from the page never fires this event.
		window.addEventListener('hashchange', () => {
			const route = routeFromHash();
			if (!route.service) {
				return;
			}

			if (!app.selected || app.selected.service !== route.service) {
				selectService(route.service, false);
			}

			if (route.operationName !== '') {
				revealOperation(app.selected, route.operationName);
			}
		});
		wireTablist([refs.tabOperations, refs.tabWsdl], activateTab);
		wireTablist([refs.wsdlTabOutline, refs.wsdlTabRaw], selectWsdlView);
		wireWsdlTools();
		refs.catalogReload.addEventListener('click', loadCatalog);
		refs.sendTimeout.addEventListener('input', refreshTimeoutNote);
		refs.wsdlOutlineFilter.addEventListener('input', applyOutlineFilter);
	}

	function wireWsdlTools() {
		refs.serviceClientCopy.addEventListener('click', () => {
			copyText(refs.serviceClientCommand.textContent, refs.serviceClientCommand).then((copied) => {
				flash(refs.serviceClientCopy, copied ? 'Copied' : 'Select and copy manually');
			});
		});
		refs.wsdlCopyUrl.addEventListener('click', () => {
			const url = app.selected ? absoluteWsdlUrl(app.selected.service) : '';
			copyText(url, null).then((copied) => {
				if (!copied) {
					// Without the clipboard the URL is shown and selected so it can be copied by hand.
					refs.wsdlStatus.textContent = url;
					selectContents(refs.wsdlStatus);
				}

				flash(refs.wsdlCopyUrl, copied ? 'Copied' : 'Copy manually');
			});
		});
		refs.wsdlDownload.addEventListener('click', () => {
			const state = app.selected;
			if (state && state.wsdl.text !== '') {
				const base = safeFileName(state.service.id, '') || safeFileName(state.service.name, 'service');
				downloadText(base + '.wsdl', state.wsdl.text);
			}
		});
	}

	// Requests the catalog. Resolves to { catalog } or, when it cannot be had, to { error } with the text to show.
	async function fetchCatalog() {
		let response;
		try {
			response = await fetch('api/services', { headers: { 'Accept': 'application/json' }, credentials: 'same-origin', cache: 'no-store' });
		} catch (error) {
			return { error: 'Could not reach the explorer API (network error).' };
		}

		if (response.redirected) {
			return { error: 'The explorer API redirected, probably to a sign-in page. Sign in and reload.' };
		}

		if (!response.ok) {
			const hint = response.status === 401 || response.status === 403 ? ' You may need to sign in or you may not be allowed to use the explorer.' : '';
			return { error: 'The explorer API answered HTTP ' + response.status + '.' + hint };
		}

		let data = null;
		try {
			const result = await readTextCapped(response, MAX_BODY_BYTES);
			data = result.truncated ? null : JSON.parse(result.text);
		} catch (error) {
			data = null;
		}

		const catalog = normalizeCatalog(data);
		return catalog ? { catalog: catalog } : { error: 'The explorer API returned an unexpected response (HTTP ' + response.status + ').' };
	}

	function defaultSnippetKind() {
		if (!snippetsAvailable()) {
			return 'curl';
		}

		return Wsdl.snippetKinds.some((kind) => kind.id === 'curl') ? 'curl' : Wsdl.snippetKinds[0].id;
	}

	// "2 services, 5 operations. SoapCore.UI 1.2.3". The version is left out when the catalog has none.
	function refreshCatalogSummary() {
		const operations = app.services.reduce((total, service) => total + service.operations.length, 0);
		const version = app.uiVersion === '' ? '' : ' SoapCore.UI ' + app.uiVersion;
		refs.catalogSummary.textContent = plural(app.services.length, 'service') + ', ' + plural(operations, 'operation') + '.' + version;
	}

	// Everything of a catalog but its services. Run for the first catalog and again when the catalog is reloaded.
	function applyCatalogSettings(catalog) {
		app.tryItOut = catalog.tryItOut;
		app.operationDetails = catalog.operationDetails;
		app.docExpansion = catalog.docExpansion;
		app.defaultVersion = catalog.defaultVersion;
		app.formEditor = catalog.formEditor;
		app.sendTimeoutSeconds = catalog.sendTimeoutSeconds;
		app.maxResponseBytes = catalog.maxResponseBytes;
		app.uiVersion = catalog.uiVersion;
		if (catalog.title) {
			document.title = catalog.title;
			refs.appTitle.textContent = catalog.title;
		}

		refs.appSubtitle.textContent = catalog.basePath ? 'Base path: ' + catalog.basePath : '';
		refs.appDescription.textContent = catalog.description;
		refs.appDescription.hidden = catalog.description === '';
		refs.headersEditor.hidden = !app.tryItOut;
		refs.sendTimeout.setAttribute('max', String(app.sendTimeoutSeconds));
		if (!app.started || Number(refs.sendTimeout.value) > app.sendTimeoutSeconds) {
			refs.sendTimeout.value = String(app.sendTimeoutSeconds);
		}

		refreshTimeoutNote();
		forEachBuiltOperation(refreshAvailability);
		forEachBuiltOperation(refreshViewSwitch);
	}

	// What is fixed for the page session: the host stylesheet and the Authorize schemes. A reload does not change them.
	function setupFixedParts(catalog) {
		app.apiKeyHeader = catalog.apiKeyHeaderName || DEFAULT_API_KEY_HEADER;
		app.authSchemes = offeredSchemes(catalog.authSchemes);
		app.snippetKind = defaultSnippetKind();
		applyHostStylesheet(catalog.stylesheetUrl);
		setupAuthorize();
	}

	function serviceKey(service) {
		return service.id !== '' ? 'id:' + service.id : 'path:' + service.path;
	}

	// The services of a reloaded catalog. A service that did not change keeps its old object, so its cards, its WSDL and the history
	// entries stay as they are. A changed service is a new object, see migrateState.
	function mergeServices(incoming) {
		const previous = new Map(app.services.map((service) => [serviceKey(service), service]));
		return incoming.map((service) => {
			const old = previous.get(serviceKey(service));
			return old && JSON.stringify(old) === JSON.stringify(service) ? old : service;
		});
	}

	// A changed service takes over the SOAP version and the envelopes the user edited, for the operations that still exist. The
	// cards of the new service are built at once when there is something to carry over.
	function migrateState(old, service) {
		const state = stateFor(service);
		if (service.soapVersions.indexOf(old.version) !== -1) {
			state.version = old.version;
		}

		const edited = old.ops.filter((ctx) => ctx.body && ctx.edited);
		if (edited.length === 0) {
			return;
		}

		buildServiceContainer(state);
		for (const previous of edited) {
			const ctx = state.ops.find((candidate) => candidate.op.name === previous.op.name);
			if (ctx) {
				adoptRequest(ctx, previous);
			}
		}
	}

	// Puts the edited request of a card of the old catalog into the card of the new one, open as it was.
	function adoptRequest(ctx, previous) {
		ensureOperationBody(ctx);
		ctx.actionInput.value = previous.actionInput.value;
		ctx.generated = true;
		ctx.generatedVersion = previous.generatedVersion;
		ctx.edited = true;
		setEnvelopeText(ctx, previous.envelope.value);
		if (previous.header.getAttribute('aria-expanded') === 'true') {
			setOperationOpen(ctx, true);
		}
	}

	function disposeState(state) {
		for (const ctx of state.ops) {
			disposeOperation(ctx);
		}
	}

	// Replaces the services of the page by the ones of a catalog. The history entries follow the services that are still there.
	function replaceServices(incoming) {
		const services = mergeServices(incoming);
		const current = new Map(services.map((service) => [serviceKey(service), service]));
		for (const [old, state] of Array.from(app.states)) {
			const next = current.get(serviceKey(old));
			if (next !== old) {
				app.states.delete(old);
				if (next) {
					migrateState(state, next);
				}

				disposeState(state);
			}
		}

		for (const entry of app.history) {
			entry.service = current.get(serviceKey(entry.service)) || entry.service;
		}

		app.services = services;
		app.byId.clear();
		for (const service of services) {
			if (service.id !== '' && !app.byId.has(service.id)) {
				app.byId.set(service.id, service);
			}
		}
	}

	// Shows the service that was selected before a reload, or the first one when it is gone. The operation that was linked stays open.
	function reselect(previous, operationName) {
		const key = previous ? serviceKey(previous.service) : '';
		const target = app.services.find((service) => serviceKey(service) === key);
		if (target && app.states.get(target) === previous) {
			markCurrentEntry(target);
			return;
		}

		selectService(target || app.services[0], false);
		if (target && operationName !== '') {
			revealOperation(app.selected, operationName);
		}
	}

	// Puts a catalog on the page. The first one also sets up what is fixed for the page session. A reload keeps the selected service and
	// operation, the envelopes that were edited, the history, the credentials of Authorize and the custom headers.
	function applyCatalog(catalog) {
		const first = !app.started;
		const previous = app.selected;
		const operationName = Wsdl.parseHashRoute(window.location.hash).operationName;
		applyCatalogSettings(catalog);
		if (first) {
			setupFixedParts(catalog);
		}

		replaceServices(catalog.services);
		app.started = true;
		buildServiceList();
		for (const state of app.states.values()) {
			refreshServiceEntry(state);
		}

		renderHistory();
		refreshCatalogSummary();
		applyServiceFilter();
		refs.emptyState.hidden = app.services.length > 0;
		refs.serviceSearch.disabled = app.services.length === 0;
		if (app.services.length === 0) {
			app.selected = null;
			refs.serviceView.hidden = true;
			return;
		}

		if (!first) {
			reselect(previous, operationName);
			return;
		}

		setSidebarOpen(!isSmallScreen());
		const route = routeFromHash();
		selectService(route.service || app.services[0], false);
		if (route.service && route.operationName !== '') {
			revealOperation(app.selected, route.operationName);
		}
	}

	// Loads the catalog at start-up and again when the Reload button is pressed. The button is off while the catalog is requested. When the
	// first load failed, the button tries again as a first load.
	async function loadCatalog() {
		const reload = app.started;
		refs.catalogReload.disabled = true;
		refs.catalogReload.setAttribute('aria-busy', 'true');
		setStatus(reload ? 'Reloading the catalog...' : 'Loading services...', 'info');
		try {
			const result = await fetchCatalog();
			if (!result.catalog) {
				setStatus((reload ? 'The catalog was not reloaded. ' : '') + result.error, 'err');
				return;
			}

			applyCatalog(result.catalog);
			if (reload) {
				announce('Catalog reloaded: ' + plural(app.services.length, 'service') + '.');
			} else {
				clearStatus();
			}
		} catch (error) {
			setStatus('The catalog could not be shown: ' + (error && error.message ? error.message : 'unexpected error'), 'err');
		} finally {
			refs.catalogReload.disabled = false;
			refs.catalogReload.removeAttribute('aria-busy');
		}
	}

	async function init() {
		cacheRefs();
		if (!Wsdl) {
			setStatus('The explorer script wsdl-parser.js did not load. Reload the page.', 'err');
			return;
		}

		refs.themeSelect.value = savedTheme();
		renderHistory();
		wireStaticEvents();
		await loadCatalog();
	}

	// The saved theme is applied as soon as the script runs, before the catalog is requested.
	applyTheme(savedTheme());
	init().catch((error) => {
		if (refs.statusBar) {
			setStatus('The explorer failed to start: ' + (error && error.message ? error.message : 'unexpected error'), 'err');
		}
	});
}());
