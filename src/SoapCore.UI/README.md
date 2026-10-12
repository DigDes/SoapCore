# SoapCore.UI

A Swagger-like explorer for [SoapCore](https://github.com/DigDes/SoapCore) SOAP endpoints.

SoapCore.UI adds one page to your ASP.NET Core application. The page lists the SOAP endpoints of the host, shows their operations, generates sample SOAP envelopes from the endpoint's WSDL and lets you call the endpoint from the browser ("Try it out").

## What it does

**Finds and documents your services**

* Lists every endpoint added with `UseSoapEndpoint` (middleware mode and endpoint-routing mode) through `ISoapEndpointRegistry`. Nothing is guessed from the route table. Endpoints that expose the same contract are grouped, and badges show the SOAP versions and the serializer.
* Shows the operations of each endpoint with the same names and SOAPAction values SoapCore uses at runtime. With [`ExposeOperationDetails`](#exposing-operation-details) it also shows the parameters (name, type, `in`/`out`/`ref`), the return type, the declared faults and the message-contract flags.
* Describes services, operations and parameters with your `[Description]` attributes and XML documentation comments (see [Descriptions](#descriptions)).
* Shows the WSDL raw and as a structured outline (services, bindings, port types, messages and types), and can copy its URL, download it or open it raw. A "Generate a client" block gives a ready-to-copy `dotnet-svcutil` command.

**Lets you try an operation**

* Generates a sample request envelope from the WSDL, for SOAP 1.1 and SOAP 1.2 (a switch appears when an endpoint exposes both). When the WSDL is not available it builds the sample from the operation details if [`ExposeOperationDetails`](#exposing-operation-details) is on, and from a generic envelope otherwise.
* Edits the request as XML (with syntax highlighting, a live well-formedness check and a Format XML button) or in a typed **Form view**: typed inputs, selects for enumerations, an include checkbox for optional elements, add and remove for repeatable elements, and nillable elements. The XML is always the source of truth, and the form is rebuilt from it every time you switch to the Form view.
* Sends the request from the browser (same origin) with Ctrl+Enter. You can edit the SOAPAction and extra headers, and set a request timeout.
* Shows the response in a viewer with status, time, size, content type, headers, search and collapsible nodes. A SOAP fault gets its own highlighted panel, and the body can be copied or downloaded.
* **Authorize** panel for Bearer, Basic, API key and WS-Security UsernameToken, kept in memory (see [The Authorize panel](#the-authorize-panel)).
* Copies the request as code in curl, PowerShell, C# `HttpClient` or raw HTTP. Secrets are shown as `REDACTED`.

**Around the page**

* A session history of the last 20 requests (memory only), deep links to one operation (`#service-id/OperationName`), Expand all / Collapse all, a Reload button that refreshes the catalog without reloading the page, and a summary of the number of services and operations with the SoapCore.UI version.
* A collapsible sidebar on small screens and a light / dark / system theme selector. The theme is remembered in the browser (`localStorage`); it is a preference, not sensitive data.
* Works without internet access: there are no CDN, font or analytics requests, and no inline scripts or styles.

## Requirements

* `net8.0`. This package targets only .NET 8.
* SoapCore **1.2.1.17 or later**. That is the first release that contains `ISoapEndpointRegistry`; SoapCore 1.2.1.16 does not have it. `AddSoapCoreUi` throws an `InvalidOperationException` with a clear message when it runs against an older SoapCore.
* Endpoint routing (`UseRouting` plus `UseEndpoints`, or `WebApplication`), because `MapSoapCoreUi` maps endpoints.
* When you protect the UI (recommended): `AddAuthorization`, `UseAuthentication` and `UseAuthorization`.

## Install

```
dotnet add package SoapCore.UI --prerelease
```

## Quick start

Minimal hosting (`WebApplication`):

```csharp
using SoapCore;
using SoapCore.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSoapCore();
builder.Services.AddSingleton<MyService>();
builder.Services.AddSoapCoreUi(options => options.Title = "My services");

var app = builder.Build();

// WebApplication is both an IApplicationBuilder and an IEndpointRouteBuilder, so UseSoapEndpoint is ambiguous.
// Cast to the one you mean: IEndpointRouteBuilder for endpoint routing, IApplicationBuilder for middleware mode.
((IEndpointRouteBuilder)app).UseSoapEndpoint<MyService>(options =>
{
	options.Path = "/Service.svc";
	options.SoapSerializer = SoapSerializer.DataContractSerializer;
});

// The explorer lists and calls your endpoints: map it in Development only, or protect it (see "Security model").
if (app.Environment.IsDevelopment())
{
	app.MapSoapCoreUi();   // serves the explorer at /soap-ui
}

app.Run();
```

`Startup` style:

```csharp
// ConfigureServices
services.AddSoapCore();
services.AddSoapCoreUi(options => options.Title = "My services");

// Configure(IApplicationBuilder app, IWebHostEnvironment env)
app.UseRouting();
app.UseEndpoints(endpoints =>
{
	endpoints.UseSoapEndpoint<MyService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
	if (env.IsDevelopment())
	{
		endpoints.MapSoapCoreUi();   // /soap-ui. Anywhere else, protect it with .RequireAuthorization()
	}
});
```

Then open `/soap-ui` (a request without the trailing slash is redirected to `/soap-ui/`).

To protect the explorer, chain the standard conventions on the builder that `MapSoapCoreUi` returns. This protects the explorer routes only, not your SOAP endpoints (see the security model below):

```csharp
// Register your authentication scheme (cookies, JWT, ...) as usual, then:
builder.Services.AddAuthorization(options =>
	options.AddPolicy("SoapUiPolicy", policy => policy.RequireAuthenticatedUser()));

// ...
app.UseAuthentication();
app.UseAuthorization();
app.MapSoapCoreUi().RequireAuthorization("SoapUiPolicy");
```

`AddSoapCoreUi` registers the default endpoint registry (`AddSoapEndpointRegistry`) only when no `ISoapEndpointRegistry` is registered yet, so the registry is filled by the `UseSoapEndpoint` calls that run later. A registry you register after `AddSoapCoreUi` must use `AddSingleton` (the last registration wins) or `Replace`.

`MapSoapCoreUi(pattern)` accepts a custom pattern such as `"/tools/soap"`. The pattern must start with `/`, have no trailing slash and use only letters, digits, `.`, `_`, `~` and `-` in its segments.

## Options

| Option | Default | Description |
|---|---|---|
| `Title` | `SoapCore Explorer` | Title shown in the page header and the browser tab. Plain text, never interpreted as HTML. 1 to 100 characters, not whitespace only, no control characters. |
| `EnableTryItOut` | `true` | When `false` the UI hides and disables sending. This is a UI convenience only, not a security boundary: the SOAP endpoints stay reachable. |
| `EndpointPathPrefix` | empty | A path prefix inserted between the request `PathBase` and each endpoint path. Use it when the SOAP endpoints are behind `app.Map(...)` or behind a reverse proxy that strips a path prefix. Empty, or `/` followed by segments of letters, digits, `.`, `_`, `~` and `-`, with no trailing slash and at most 200 characters. |
| `ExposeOperationDetails` | `false` | Adds the parameters, return type, declared faults and message-contract flags of each operation to the explorer API, so the UI can show them (see [Exposing operation details](#exposing-operation-details)). Opt-in because it reveals CLR type names. |
| `Description` | `null` | A subtitle under the title. Plain text, never interpreted as HTML. At most 300 characters, no control characters (so no line breaks). `null`, empty or whitespace only means no subtitle. |
| `DefaultSoapVersion` | `null` | `"1.1"` or `"1.2"`. The explorer preselects that SOAP version on the endpoints that expose it. `null` means automatic. |
| `DocExpansion` | `List` | `SoapUiDocExpansion.List` lists every operation collapsed. `SoapUiDocExpansion.Full` expands every operation card when a service is selected. |
| `AuthSchemes` | `All` | The schemes of the Authorize panel: a combination of `SoapUiAuthSchemes.Bearer`, `Basic`, `ApiKey` and `WsUsernameToken`. `All` offers all four, `None` hides the panel. |
| `ApiKeyHeaderName` | `X-Api-Key` | The header the API key scheme sends. 1 to 128 letters, digits, `-` or `_`. Headers that browsers do not let a page set (`Host`, `Cookie`, `Origin`, `Referer`, `Content-Length`, `Proxy-*`, `Sec-*`, `Access-Control-Request-*`, ...) and `Content-Type` and `SOAPAction`, which the explorer sets itself, are rejected. Not a secret. |
| `StylesheetUrl` | `null` | An extra style sheet of the same origin, loaded after the built-in one, to follow your branding. A path that starts with a single `/` (`/branding.css`, optionally with a query), at most 200 characters. Absolute and scheme-relative URLs are rejected. |
| `UseDescriptionAttributes` | `true` | Reads `[Description]` from the service type, its contract interfaces, the operation methods and their parameters (see [Descriptions](#descriptions)). |
| `EnableFormEditor` | `true` | When `false` the explorer hides the Form view of the request and only the XML view is available. A UI convenience only (see [Form view, timeout and response limits](#form-view-timeout-and-response-limits)). |
| `SendTimeoutSeconds` | `60` | The timeout, in seconds, of one request sent from the explorer. It is the default of the timeout field of the UI and the largest value the user can enter there (the user can lower it). From 1 to 600. A limit of the page, not a protection of the server. |
| `MaxResponseBytes` | `5242880` (5 MiB) | The largest response body the page reads and shows. A larger response is cut. From 1024 to 52428800 (50 MiB). A limit of the page, not a protection of the server. |
| `IncludeXmlComments(path)` | none | Method. Adds an XML documentation file whose `summary` and `param` texts describe services, operations and parameters. Call it once per file; the files are listed in the read-only `XmlCommentsFiles`. |

Invalid option values make `MapSoapCoreUi` throw an `OptionsValidationException` at startup.

### Exposing operation details

By default `/soap-ui/api/services` only lists the operations (name, SOAPAction, one-way flag, contract). With `options.ExposeOperationDetails = true` each operation also carries:

* `parameters`: the name (as it appears in the SOAP message), the CLR type and the direction (`in`, `out` or `ref`) of each parameter. At most 100 are listed, and parameters that are not part of the SOAP message (`CancellationToken`, `HttpContext`) are skipped.
* `returns`: the CLR type of the result, or `void` for one-way operations and for methods that return `Task` or `ValueTask`. `Task<T>` and `ValueTask<T>` are reported as `T`.
* `faults`: the CLR types declared with `[FaultContract]`, each listed once, at most 20.
* `messageContract`: whether the request and the response are message contracts (`{ "request": false, "response": false }`).

Type names are display names such as `System.Collections.Generic.List<MyApp.Item>`. They never contain the assembly name, version or public key token.

**Why it is opt-in:** these names reveal the namespaces and class names behind your contracts to anyone who can open the explorer. The WSDL already describes the XML shape of the contract, but not the .NET types behind it. Turn it on when the explorer is as private as the code of the service (Development, or behind `RequireAuthorization()`). When it is off, the four members are `null` in every operation and `operationDetailsEnabled` is `false` in the response. The value is read on every request, so switching it off hides the details at once.

```csharp
builder.Services.AddSoapCoreUi(options =>
{
	options.Title = "My services";
	options.ExposeOperationDetails = true;   // Development or protected explorers only
});
```

The description of a service and of an operation is served whatever `ExposeOperationDetails` is (see [Descriptions](#descriptions)). The description of a parameter belongs to the parameter, so it is only served with the details.

### Descriptions

The explorer shows a short description for each service, operation and parameter that has one. They are read from two places, in this order:

1. `[System.ComponentModel.Description]` on the service type, on its contract interface, on the operation methods and on their parameters (`UseDescriptionAttributes`, on by default).
2. The XML documentation of the same members: the `summary` of a type or a method and the `param` of a parameter, from the files you add with `options.IncludeXmlComments(path)`.

The contract is consulted before the service type that implements it, and an attribute wins over XML comments. A description that is empty counts as none.

```csharp
// In the csproj: <GenerateDocumentationFile>true</GenerateDocumentationFile>
// (add <NoWarn>$(NoWarn);1591</NoWarn> if you do not document every member)
builder.Services.AddSoapCoreUi(options =>
{
	options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "MyService.xml"));
	options.ExposeOperationDetails = true;   // the descriptions of parameters are part of the details
});
```

```csharp
[ServiceContract(Namespace = "urn:demo")]
public interface IDemoService
{
	/// <summary>Adds two numbers.</summary>
	/// <param name="left">The first number.</param>
	/// <param name="right">The second number.</param>
	[OperationContract]
	int Add(int left, int right);

	[Description("Parses a number. This one uses the attribute instead of an XML comment.")]
	[OperationContract]
	bool TryParse(string text, [Description("The number, when the text is one.")] out int value);
}
```

* **Plain text only.** Nothing that leaves the server is HTML or Markdown, and the page writes it with `textContent`. From the XML, the text of `summary` is used: `<see cref="X"/>` becomes the last segment of the reference (`X`), `<paramref name="p"/>` becomes `p`, `<c>x</c>` becomes `x`, and runs of whitespace and line breaks become one space. A description is at most 500 characters: a longer one is cut and ends with `...`.
* **Read lazily, once.** An XML file is read the first time a service is described, and never again. The result is cached with the operations of the service type, so the reflection and the files are read once per type, and a change to the options after the first request does not change a description that was already read.
* **A broken file never breaks the explorer.** A file that is missing, unreadable or not valid XML is skipped, and one warning is logged (`Could not read the XML documentation file ...`). The explorer then goes on without its descriptions. The files are read as untrusted input: a DTD is refused (so no entity is ever resolved), no external resource is loaded and the size is capped. The path of the file is never sent to the browser.
* **The file has to be there at run time.** `GenerateDocumentationFile` writes `MyService.xml` next to the assembly, and a normal publish copies it. A relative path is resolved against the current directory of the process, so prefer `Path.Combine(AppContext.BaseDirectory, ...)`.
* **Generic types, nested types and overloads** use the member ids that the compiler writes (`T:Ns.Outer.Inner`, `M:Ns.Type.Method(System.String,System.Int32)`, `` `0 `` for the type parameters of a generic contract). For an operation the explorer also looks at the implementing method of the service type when the contract does not describe it.
* **Descriptions are served to everyone who can open the explorer.** Keep internal notes out of the text you want shown, or leave the XML comments out of `IncludeXmlComments`.

### The Authorize panel

The explorer has an **Authorize** panel, as Swagger UI does, so a service that needs credentials can be tried without hand-writing headers. `AuthSchemes` chooses what it offers (`SoapUiAuthSchemes.None` hides it):

* **Bearer**: you paste a token and the explorer sends `Authorization: Bearer <token>`.
* **Basic**: you type a user name and a password and the explorer builds `Authorization: Basic <base64>`.
* **API key**: you paste a key and the explorer sends it in the header named by `ApiKeyHeaderName` (`X-Api-Key` by default).
* **WS-Security UsernameToken**: you type a user name and a password, and choose text or digest. The explorer inserts a `wsse:Security` header with a `wsse:UsernameToken` into the `<soapenv:Header>` of the envelope. With `PasswordText` the password is sent as is. With `PasswordDigest` it sends a fresh nonce, the creation time and `Base64(SHA1(nonce + created + password))`.

```csharp
builder.Services.AddSoapCoreUi(options =>
{
	// Offer only what your endpoints understand.
	options.AuthSchemes = SoapUiAuthSchemes.Bearer | SoapUiAuthSchemes.ApiKey;
	options.ApiKeyHeaderName = "X-Company-Key";
});
```

What to know:

* **In memory only.** The credentials are forgotten when the page is reloaded. They are never written to `localStorage` or to a cookie, never put in a URL and never logged.
* **Applied to every request of the page:** the WSDL request and each Try it out call, so an endpoint that needs credentials for its WSDL works too.
* **Redacted in code snippets.** The `Authorization` header value, the API key value and the password of a WS-Security UsernameToken are shown as `REDACTED` in the copied curl, PowerShell, C# and raw HTTP code. The body of the request is not redacted, so check a snippet before you share it.
* **How it relates to SoapCore's WS-Security filter.** The UsernameToken the explorer builds is what `services.AddSoapWsSecurityFilter(username, password)` (the `WsMessageFilter`) validates, as plain text and as a digest with nonce and created. The explorer does not know your server credentials: you type them.
* **It is a convenience, not an authentication boundary.** The panel only adds headers to the requests of the explorer. Your SOAP endpoints must check them, and `AuthSchemes` does not restrict what the endpoints accept.
* **Use HTTPS.** A bearer token, a Basic password, an API key and a `PasswordText` token travel in clear text over plain HTTP, and a digest does not make a captured request safe to replay. Do not type production credentials into an explorer that is served over HTTP or that other people can open.
* The generic **custom headers** editor stays available for anything else.

A runnable demo, with an endpoint that accepts each scheme, is the `/Secure.svc` endpoint of the [sample](#sample).

### Form view, timeout and response limits

```csharp
builder.Services.AddSoapCoreUi(options =>
{
	options.EnableFormEditor = true;              // the default; false hides the Form view
	options.SendTimeoutSeconds = 30;              // default and maximum timeout of one request sent from the page (1 to 600)
	options.MaxResponseBytes = 2 * 1024 * 1024;   // the page reads at most 2 MiB of a response (1024 to 52428800)
});
```

* **`EnableFormEditor`** (default `true`). The Form view shows the request as typed fields and always rebuilds from the XML, which stays the source of truth. With `false` the explorer hides it and only the XML view is left. It changes what the page shows and nothing on the server.
* **`SendTimeoutSeconds`** (default `60`, from 1 to 600) is both the default of the timeout field and the largest value the user can enter there. The user can lower the timeout in the page, but cannot raise it above what you configured.
* **`MaxResponseBytes`** (default `5242880`, from 1024 to 52428800) is the largest response body the page reads. A larger body is cut and the page says so.
* **They are limits of the page, not protections of the server.** The browser enforces both: when the timeout expires it stops waiting and cancels the request, and when the limit is reached it stops reading. The SOAP endpoint may keep working on the request, and it has already produced the whole response. They say nothing about what other clients can send or read, so protect the server with the limits of the web server (request timeouts, maximum request body size) and of your own service.
* Out-of-range values make `MapSoapCoreUi` throw an `OptionsValidationException` at startup. All three options are read on every request.
* The page receives the values in the explorer API (`/soap-ui/api/services`) as `formEditorEnabled`, `sendTimeoutSeconds` and `maxResponseBytes`, and also `uiVersion`: the version of SoapCore.UI without build metadata. That API is internal to the explorer: its members are only ever added, but do not build other clients on it.

### Customizing the page

```csharp
builder.Services.AddSoapCoreUi(options =>
{
	options.Title = "Orders API";
	options.Description = "SOAP services of the orders team. Staging.";   // subtitle, plain text
	options.DefaultSoapVersion = "1.2";                                   // preselected when the endpoint exposes SOAP 1.2
	options.DocExpansion = SoapUiDocExpansion.Full;                       // operation cards start expanded
	options.StylesheetUrl = "/branding/explorer.css";                     // same origin, loaded after the built-in style sheet
});
```

* **`DefaultSoapVersion`** is only a preselection. An endpoint that does not expose that version keeps the automatic choice, and the user can always switch.
* **`DocExpansion`**: `List` (the default) shows each operation collapsed, `Full` expands every operation card as soon as a service is selected. There are also Expand all / Collapse all buttons.
* **`StylesheetUrl`** has to be a path of the same origin, because the page has a strict `Content-Security-Policy` (`style-src 'self'`): `/branding.css`, `/css/explorer.css?v=3`. Absolute URLs, `//host/...` and anything with a backslash, a space or a `#` are rejected at startup. The value is used exactly as written, so it is not combined with the `PathBase`. Serve the file yourself, for example with `UseStaticFiles`.

## Security model

SoapCore.UI is a development and diagnostics tool. Read this section before you map it on a host that anyone else can reach.

* **No server-side proxy.** The browser calls the SOAP endpoints itself, same origin, with `fetch`, and loads the WSDL itself. The server never makes an outgoing request on behalf of the page, so there is no SSRF surface. The explorer can only reach endpoints on its own origin.
* **Protect the UI with the standard endpoint conventions.** `MapSoapCoreUi` returns an `IEndpointConventionBuilder` (a route group), so chain what you need, for example `app.MapSoapCoreUi().RequireAuthorization("SoapUiPolicy")`, `.RequireHost("localhost")` or `.RequireCors("my-policy")`. The conventions apply to every route of the explorer.
* **The conventions cover the UI routes only.** `RequireAuthorization()` on `MapSoapCoreUi()` does not protect your SOAP endpoints. SoapCore's own routes are not covered by it. In endpoint-routing mode the builder returned by `UseSoapEndpoint` covers only the base route of the endpoint, and `{path}/{operation}` and `$metadata` get no convention. Authenticate and authorize the SOAP endpoints on their own.
* **GET and form-POST invocation.** The explorer needs `HttpGetEnabled` (and `HttpsGetEnabled` on https) on the SOAP endpoints to show the WSDL and to generate samples. In SoapCore, enabling GET also lets `GET {path}/{Operation}?p=...` invoke operations, and a form POST to `{path}/{Operation}` invokes them too (that one is controlled by `HttpPostEnabled`). For endpoints that authenticate with cookies or with integrated (Windows or Negotiate) authentication, these routes can be abused by cross-site requests (CSRF), independently of the explorer. Setting `HttpPostEnabled = false` does not protect the SOAP POST to the endpoint root.
  **Recommendation:** map the UI only in Development or staging. In production set `HttpGetEnabled = false` and `HttpPostEnabled = false` on the SOAP endpoints (and the `Https` variants), and do not map the explorer.
* **Cross-origin reads are off.** The explorer is same-origin by design, and its own requests never carry an `Origin` header. A GET or HEAD request that carries an `Origin` header for another origin is answered with 403, even when the host has a permissive host-wide CORS policy (`UseCors()` with a default policy). Chaining `.RequireCors(policy)` on `MapSoapCoreUi()` is an explicit opt-in and lifts this check.
* **The `Origin` check does not stop DNS rebinding.** After a rebinding attack the attacker's page is same-origin with your host name, so it sends no foreign `Origin` and can read the explorer and call your endpoints with the victim's browser. Restrict the host names the application accepts: set `AllowedHosts` (the default ASP.NET Core template ships `*`, which accepts any host) and, for the explorer alone, chain `.RequireHost("localhost")` on `MapSoapCoreUi()`. This matters most for an explorer that runs on a developer machine.
* **Response headers.** Every response written by the UI carries a strict `Content-Security-Policy` (`default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'; object-src 'none'`), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY` and a `Cache-Control` header. Responses written by ASP.NET Core itself (401, 403, 404, 405) do not carry them.
* **Warning when left open.** The UI logs a warning on the first request when it is served without authorization outside the Development environment: no `RequireAuthorization()` on `MapSoapCoreUi()` and no authorization `FallbackPolicy`. It never blocks the request. Map the explorer in Development only, or chain `RequireAuthorization()`.
* **Try it out uses your session.** Requests are sent with same-origin credentials, so the user's session cookie or integrated credentials accompany every call, exactly as if the user had made the call from the same browser.
* **`EnableTryItOut = false` is not authorization.** It only disables the controls of the UI. Anyone can still call the endpoints.
* **Credentials stay in memory.** What you type in the Authorize panel and in the custom headers editor is never stored, logged or put in the URL (see [The Authorize panel](#the-authorize-panel)). A copied code snippet replaces the values of headers whose name looks like a credential (it contains `auth`, `cookie`, `token`, `key`, `secret`, `pass`, `pwd`, `cred`, `session`, `sid`, `jwt`, `bearer`, `sig`, `csrf` or `xsrf`) and the password of a WS-Security UsernameToken with `REDACTED`. This is a name heuristic, and the rest of the request body is **not** redacted, so check a snippet before you share it.
* **Faults can leak details.** By default SoapCore puts the raw exception message into SOAP faults. Register `services.AddSoapExceptionTransformer(ex => ...)` to return a generic text for unexpected exceptions.
* **The list is not filtered.** The explorer lists every registered endpoint. It does not honor `HttpGetEnabled = false` or `[Authorize]` on operations, because the registry does not expose them.
* **What anyone who can open the explorer sees:** the endpoints, their operations and SOAPAction values, the WSDL, the descriptions you wrote and, with `ExposeOperationDetails`, the CLR type names behind your contracts. Keep the explorer as private as the code of the service.

## Limitations

* **`app.Map` and `UsePathBase` prefixes are not in the registry.** The registry records the path exactly as configured. The explorer adds the `PathBase` of the current request automatically, and `EndpointPathPrefix` covers `app.Map(...)` branches and reverse proxies that strip a prefix. One prefix applies to all endpoints, so it must match how all of them are hosted.
* **Map the UI in the same pipeline branch as the SOAP endpoints.**
* **The `{pattern}` to `{pattern}/` redirect needs the proxy to preserve the trailing slash.** The page uses relative asset URLs, so it must be served at `{pattern}/`.
* **The WSDL view and the sample generation need `HttpGetEnabled`** (`HttpsGetEnabled` for https; both default to `true`). Without them the endpoint answers 403 to the WSDL request, and the explorer builds the sample from the operation details (with `ExposeOperationDetails`) or from a generic envelope.
* **Same origin only.** The explorer cannot call endpoints on another origin.
* **Do not map the UI under the path of a middleware-mode SOAP endpoint.** That middleware answers first, with a 500. Use a sibling path such as `/soap-ui`. A routing-mode endpoint at `/soap` with the UI at `/soap/ui` works.
* **Case.** SoapCore matches the endpoint path with the casing you configured unless `CaseInsensitivePath` is set; a different casing gives a 500, not a 404.
* **`MessageVersion.None` endpoints are listed, but cannot be called from the explorer.** They have no WSDL and no SOAP 1.1/1.2 envelope, so the explorer has nothing to generate and the Send button stays disabled.
* **File-based WSDL (`WsdlFileOptions`).** Imports are not followed in this release.
* **Overloads.** Operations that resolve to the same name are shown once.
* **XmlSerializer and `Guid` / `DateTimeOffset`.** SoapCore declares `DateTimeOffset` (and `Guid`) as `xsd:string` in the WSDL it generates for `SoapSerializer.XmlSerializer`, so the WSDL says nothing about the real type. The explorer guesses a sample value from the name of the element, which is a guess: if the server rejects the value, edit it in the envelope before you send it.
* **DataContract namespaces that start with `urn:`.** SoapCore's WSDL writer does not prefix the references to a type correctly when its DataContract namespace starts with `urn:`: it writes the schema of the type under `http://schemas.datacontract.org/2004/07/...`, but the references to the type without that prefix, so the WSDL is inconsistent. The sample envelope then contains `unresolved type` comments instead of the members of that type: fill them in by hand, or use `http://...` data namespaces.
* **`dotnet-svcutil` and SOAP 1.1 + 1.2 with the DataContractSerializer.** When an endpoint has both a SOAP 1.1 and a SOAP 1.2 encoder and you do not set `BindingName` / `PortName` on the encoders, SoapCore gives both bindings (and both ports) the same name in the WSDL, and `dotnet-svcutil` fails with `More than one binding named ...`. This is a SoapCore WSDL issue, not an explorer issue. Name the SOAP 1.2 encoder, for example `new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10, BindingName = "Soap12Binding_IMyService", PortName = "Soap12Binding_IMyService" }`. The XmlSerializer writer adds a suffix and is not affected.
* **Descriptions come from your code, not from the WSDL.** Only `[Description]` and the `summary` / `param` of the XML comments are used (not `remarks`, `returns` or `example`), and an `<inheritdoc/>` is not followed.
* **The Form view edits one request element tree at a time and does not model `xs:choice` or `xs:any`.** It does not enforce that only one branch of an `xs:choice` is filled in, and it shows `xs:any` and similar open content read-only. Edit those parts in the XML view: the XML is always what is sent.
* **The Form view cannot represent mixed content** (text interleaved with child elements). Use the XML view for it.
* **Very large requests (over 256 KB) are shown without syntax highlighting**, to keep the editor responsive. They can still be edited and sent.

Non-goals: no per-endpoint filtering and no server-side proxy.

## Troubleshooting

* **The service list is empty.** The registry is not filled. Check that `AddSoapCoreUi()` runs before the endpoints are registered (it is in `ConfigureServices`, or before `builder.Build()`), and that the endpoints are registered with `UseSoapEndpoint`.
* **The WSDL tab shows 401, 403 or 500.** 401 or 403: `HttpGetEnabled` / `HttpsGetEnabled` is `false` on that endpoint, or the endpoint needs credentials (use the Authorize panel or the custom headers editor: the explorer requests the WSDL again when they change). 500: SoapCore could not generate a WSDL, for example the message version is `None` or the service type has no `[ServiceContract]`. 404 usually means a path prefix is missing (see `EndpointPathPrefix`). Without the WSDL the explorer builds the sample from the operation details when `ExposeOperationDetails` is on, and from a generic envelope otherwise; message version `None` has no envelope at all.
* **No descriptions appear.** Check that `UseDescriptionAttributes` is on or that `IncludeXmlComments` points at a file that exists at run time (look for the warning `Could not read the XML documentation file` in the log), that the project has `GenerateDocumentationFile`, and that the member really has a `summary`. Parameter descriptions also need `ExposeOperationDetails`.
* **The Authorize panel is missing.** `AuthSchemes` is `None`. Set it to `SoapUiAuthSchemes.All`, or to the schemes you want.
* **The Form view is missing.** `EnableFormEditor` is `false`. Set it to `true` (the default).
* **A response is cut, or Send gives up after a while.** The page reads at most `MaxResponseBytes` of a response and waits at most as long as its timeout field says, which is never more than `SendTimeoutSeconds`. Raise the options for large or slow operations. Neither limit affects the SOAP endpoint.
* **Startup throws an `OptionsValidationException`.** Read its message: it lists every invalid option (for example a `StylesheetUrl` that is not a path of the same origin, an `ApiKeyHeaderName` that browsers do not let a page set, a `SendTimeoutSeconds` outside 1 to 600 or a `MaxResponseBytes` outside 1024 to 52428800).
* **Send returns a fault with "Parameter count mismatch".** SoapCore selects the message encoder from the `Content-Type`, and a mismatched envelope version gives this misleading fault. The explorer pairs the envelope namespace and the `Content-Type` for you, so look for an edited envelope that still uses the other SOAP version.

## Sample

A runnable sample with six endpoints (DataContract and XmlSerializer, SOAP 1.1 and 1.2, a middleware-mode endpoint, a service with faults and a one-way operation, XML descriptions, and a `/Secure.svc` endpoint that accepts a bearer token, Basic, an API key or a WS-Security UsernameToken, to try the Authorize panel) is in [samples/ServerWithUi](https://github.com/DigDes/SoapCore/tree/master/samples/ServerWithUi). Its credentials are public demo values.

## License

MIT
