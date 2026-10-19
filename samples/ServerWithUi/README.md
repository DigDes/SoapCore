# ServerWithUi sample

A SoapCore host that uses the **SoapCore.UI** explorer, a Swagger-like page for SOAP services. The explorer lists the endpoints registered with SoapCore, shows their operations, shows their descriptions, generates sample SOAP envelopes from the WSDL, lets you authorize (bearer, Basic, API key, WS-Security) and lets you call the endpoints from the browser ("Try it out").

The sample references `SoapCore`, `SoapCore.UI` and `Models` as projects, so it always runs against the code in this repository.

## Run

Requires the .NET 8 SDK.

```bash
dotnet run --project samples/ServerWithUi
```

Then open <http://localhost:5290/soap-ui>. The `http` launch profile does this for you and sets `ASPNETCORE_ENVIRONMENT=Development`, which is what turns the explorer on (see [Security notes](#security-notes)).

### Path base demo

The sample can run behind a path base, to show that the explorer and the endpoint URLs follow it:

- Launch profile: `dotnet run --project samples/ServerWithUi --launch-profile http-pathbase`
- bash / zsh on Linux or macOS: `PathBase=/demo dotnet run --project samples/ServerWithUi`
- PowerShell: `$env:PathBase="/demo"; dotnet run --project samples/ServerWithUi`
- Git Bash on Windows: `MSYS_NO_PATHCONV=1 PathBase=/demo dotnet run --project samples/ServerWithUi`

Use the environment variable form: Git Bash on Windows rewrites a value that starts with `/` (also in an argument such as `--PathBase=/demo`) into a Windows path, and the sample then ignores it and runs without a path base. `MSYS_NO_PATHCONV=1` switches the rewrite off.

Then open <http://localhost:5290/demo/soap-ui>. The endpoints are served at `/demo/Service.svc` and so on.

## Endpoints

| Path | Contract | Mode | Serializer | SOAP | Notes |
|---|---|---|---|---|---|
| `/Service.svc` | `Models.ISampleService` | routing | DataContractSerializer | 1.1 + 1.2 | |
| `/Service.asmx` | `Models.ISampleService` | routing | XmlSerializer | 1.1 | See [Known quirk](#known-quirk-xmlserializer-and-guid--datetimeoffset) |
| `/Weather.svc` | `IWeatherService` | routing | DataContractSerializer, `CaseInsensitivePath = true` | 1.1 + 1.2 | Complex types, lists, enum, dates, a fault, a one-way operation |
| `/Legacy/Weather.asmx` | `IWeatherService` | **middleware** | XmlSerializer | 1.1 | Proves the registry also sees middleware mode |
| `/NoWsdl.svc` | `IHeaderDemoService` | routing | DataContractSerializer, `GenerateSoapActionWithoutContractName = true`, GET disabled | 1.1 | The WSDL request is refused (403), so the explorer builds the sample from the operation details |
| `/Secure.svc` | `ISecureService` | routing | DataContractSerializer, `CaseInsensitivePath = true` | 1.1 + 1.2 | Needs credentials. See [`/Secure.svc` and the Authorize panel](#securesvc-and-the-authorize-panel-demo-only) |

The same contract is exposed at two paths on purpose (`ISampleService` and `IWeatherService`). The explorer gives each endpoint its own id.

## What to try

- **SOAP 1.1 / 1.2 switch.** Open `/Service.svc` or `/Weather.svc`. The version switch appears because both versions are registered. The explorer pairs the envelope namespace with the `Content-Type` (and the `SOAPAction` header for 1.1).
- **DataContract vs XmlSerializer.** Open the WSDL tab of `/Service.svc`, then `/Service.asmx`, and compare how the same contract is described.
- **Middleware mode.** `/Legacy/Weather.asmx` is registered with `UseSoapEndpoint` on the application builder. It never shows up in the endpoint data source, yet it is listed because the explorer reads `ISoapEndpointRegistry`.
- **A SOAP fault.** Call `GetDailyForecast` or `GetReport` on `/Weather.svc` with an unknown city code (the known codes are `BOG`, `MDE`, `NYC` and `LON`). The response panel highlights the fault. The fault text comes from the `FaultException` the service throws.
- **A one-way operation.** `SubscribeToAlerts` returns HTTP 202 with no body.
- **Generated samples.** `GetReport` takes a complex request with an optional date, `GetForecast` takes a string, a date and an int, and `SubscribeToAlerts` takes a complex type. The generated `CityCode` is the placeholder `string`, which is not a known city: `GetReport` and `GetDailyForecast` then answer with a fault (a handy first look at the fault highlighting), `GetForecast` with an empty list, and `SubscribeToAlerts` with HTTP 202. Replace the placeholder with `BOG`, `MDE`, `NYC` or `LON` and send again: the response then contains nested types, lists, an enum, a `Guid` and dates.
- **No WSDL.** Open `/NoWsdl.svc`. The WSDL tab explains that GET is disabled, and the sample envelopes are built from the operation details (the sample turns `ExposeOperationDetails` on), with one element per parameter instead of a generic body. Its SOAP actions have no contract name (`GenerateSoapActionWithoutContractName`).
- **Custom headers.** On `/NoWsdl.svc`, `EchoWithToken` fails with a fault. Open "Custom request headers", add `X-Demo-Token` with the value `demo` and send again. Headers live in memory only, and they are also sent with the WSDL request, so an endpoint that needs a header to serve its WSDL works too (the explorer requests the WSDL again when the headers change).
- **Copy code.** Every operation can be copied as curl (bash), PowerShell, C# `HttpClient` or a raw HTTP message. Values of sensitive headers, the Authorize credentials and the WS-Security password are replaced with `REDACTED`, but the rest of the request body is not redacted.
- **Form view and response viewer.** Open `PingComplexModel` on `/Service.asmx` and switch the request to **Form**: edit the typed fields, include or remove optional elements and add items to a list, then switch to **XML** to see the result and send. The response viewer has a search box and collapsible nodes, and the WSDL tab has a structured **Outline**.
- **Descriptions.** The page shows a subtitle (`SoapUiOptions.Description`), and `/Weather.svc` and `/Secure.svc` show a description for the service, each operation and each parameter. They come from the XML documentation of this project (`GenerateDocumentationFile` in the csproj and `options.IncludeXmlComments(...)` in `Program.cs`) and, for `CityExists` and `SubscribeToAlerts`, from `[Description]` attributes, which need no XML file. The parameter descriptions are part of the operation details, so they show because `ExposeOperationDetails` is on.
- **Authorize.** Open `/Secure.svc`: calling it without credentials answers HTTP 401 with a SOAP fault. Press **Authorize**, pick a scheme and enter the demo value from the next section: the same call then succeeds, and `WhoAmI` tells which scheme you used.

## `/Secure.svc` and the Authorize panel (DEMO ONLY)

`/Secure.svc` (`ISecureService`: `WhoAmI`, `Echo`, `Add`) only answers callers that authenticate. It accepts **any one** of four schemes, the four schemes of the **Authorize** panel of the explorer. The check is `Secure/SecureDemoMessageProcessor.cs`, a SoapCore message processor registered with `AddSoapMessageProcessor`. It does nothing for the other endpoints.

> **These credentials are public demo values, printed here on purpose. Never use them, or this code, to protect a real service.**

| Scheme | What the request carries | Demo value |
|---|---|---|
| Bearer | `Authorization: Bearer <token>` | token `demo-token` |
| Basic | `Authorization: Basic <base64 of user:password>` | user `demo`, password `demo-password` |
| API key | the header `X-Api-Key: <key>` (the name is `SoapUiOptions.ApiKeyHeaderName`, set in `Program.cs`) | key `demo-key` |
| WS-Security UsernameToken | a `wsse:Security` SOAP header (below) | user `demo`, password `demo-password` |

The HTTP headers are checked first. When none matches and the message has a `Security` header, the UsernameToken is checked with `WsMessageFilter`, the same validation that `AddSoapWsSecurityFilter` gives an endpoint (it accepts `PasswordText`, and `PasswordDigest` with `Nonce` and `Created`). Anything else gets HTTP 401 and a SOAP fault. The WSDL (`GET /Secure.svc?wsdl`) is public, because the explorer needs it.

**In the explorer:** open `/Secure.svc`, press **Authorize**, choose a scheme, type the demo value and apply it. The credentials stay in memory only (reloading the page forgets them), they are sent with the WSDL request and with every Try it out, and they are shown as `REDACTED` in the generated code. For WS-Security the explorer inserts the `wsse:Security` header into the envelope.

**From the command line** (bash; `URL`, `ACTION` and `ENVELOPE` are reused by all the examples):

```bash
URL=http://localhost:5290/Secure.svc
ACTION='"http://soapcore.dev/samples/secure/ISecureService/WhoAmI"'
ENVELOPE='<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:sec="http://soapcore.dev/samples/secure"><soapenv:Header/><soapenv:Body><sec:WhoAmI/></soapenv:Body></soapenv:Envelope>'

# Bearer
curl -s "$URL" -H 'Content-Type: text/xml; charset=utf-8' -H "SOAPAction: $ACTION" -H 'Authorization: Bearer demo-token' -d "$ENVELOPE"

# Basic (curl builds the Authorization header)
curl -s "$URL" -H 'Content-Type: text/xml; charset=utf-8' -H "SOAPAction: $ACTION" -u demo:demo-password -d "$ENVELOPE"

# API key
curl -s "$URL" -H 'Content-Type: text/xml; charset=utf-8' -H "SOAPAction: $ACTION" -H 'X-Api-Key: demo-key' -d "$ENVELOPE"

# No credentials: HTTP 401 and a SOAP fault
curl -si "$URL" -H 'Content-Type: text/xml; charset=utf-8' -H "SOAPAction: $ACTION" -d "$ENVELOPE"
```

A WS-Security UsernameToken goes in the SOAP header. With the password as plain text:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                  xmlns:sec="http://soapcore.dev/samples/secure"
                  xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
  <soapenv:Header>
    <wsse:Security>
      <wsse:UsernameToken>
        <wsse:Username>demo</wsse:Username>
        <wsse:Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText">demo-password</wsse:Password>
      </wsse:UsernameToken>
    </wsse:Security>
  </soapenv:Header>
  <soapenv:Body><sec:WhoAmI/></soapenv:Body>
</soapenv:Envelope>
```

With a digest, the password is never sent: `Password = Base64(SHA1(nonce bytes + Created text + password))`, and the nonce is sent Base64 encoded. This is a worked value for the nonce `demo-nonce-001` and the creation time `2025-01-01T12:00:00Z`:

```xml
<wsse:UsernameToken xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
  <wsse:Username>demo</wsse:Username>
  <wsse:Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest">tdRoepUD6iVqetjZXkbv0bEIK8g=</wsse:Password>
  <wsse:Nonce>ZGVtby1ub25jZS0wMDE=</wsse:Nonce>
  <wsu:Created>2025-01-01T12:00:00Z</wsu:Created>
</wsse:UsernameToken>
```

SoapCore only checks that the digest matches. It does not reject an old `Created` or a repeated nonce, so a captured digest can be replayed: use HTTPS and a real implementation in production.

## Known quirk: XmlSerializer and Guid / DateTimeOffset

The WSDL that SoapCore generates for the XmlSerializer declares `Guid` and `DateTimeOffset` members as `xsd:string`, so the WSDL says nothing about the real type. The explorer recognises a `DateTimeOffset` by the name of the element (it contains `DateTimeOffset`) and fills it with an ISO 8601 value, so `PingComplexModel` on `/Service.asmx` works with the generated sample. A `Guid` member cannot be recognised that way and gets the generic value `string`: if the server rejects it, edit the value before you send it. The same operation on `/Service.svc` (DataContractSerializer) describes the types properly.

## Known quirk: SOAP 1.1 and 1.2 binding names (dotnet-svcutil)

The explorer shows a `dotnet-svcutil` command for every service ("Generate a client"). For an endpoint that uses the DataContractSerializer with both a SOAP 1.1 and a SOAP 1.2 encoder, SoapCore names the WSDL binding and the port of both encoders after the contract, so they get the same name and `dotnet-svcutil` fails with `More than one binding named "BasicHttpBinding_..."`. The XmlSerializer writer adds a `_soap` / `_soap12` suffix and is not affected.

The sample avoids it by naming the SOAP 1.2 encoder (`BindingName` and `PortName`, see `Soap11And12` in `Program.cs`). Do the same in your own endpoints:

```csharp
options.EncoderOptions = new[]
{
	new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11 },
	new SoapEncoderOptions
	{
		MessageVersion = MessageVersion.Soap12WSAddressing10,
		BindingName = "Soap12Binding_IMyService",
		PortName = "Soap12Binding_IMyService",
	},
};
```

## Security notes

- **The explorer is on only in Development**, or when `SoapUi:Enabled` is `true` (for example `SoapUi__Enabled=true`). Setting it to `true` exposes the explorer, and with it the GET handling of the endpoints, to anyone who can reach the host.
- **Protect it.** Chain standard endpoint conventions on `MapSoapCoreUi()`: `.RequireAuthorization()`, `.RequireHost(...)`, `.RequireCors(...)`. Commented examples are in `Program.cs`. They protect the explorer routes only. They do not protect the SOAP endpoints, which you secure separately.
- **GET on the endpoints.** The explorer needs GET to fetch `?wsdl`. In SoapCore that setting also enables `GET {path}/{Operation}?p=...`, which executes operations. The sample enables GET only while the explorer is enabled, and always disables the form POST route (`HttpPostEnabled = false`, `HttpsPostEnabled = false`). A SOAP POST to `{path}` keeps working.
- **`AllowedHosts` is limited to localhost** in `appsettings.json`. A wildcard would allow DNS rebinding against a sample running on your machine. Change it when you host the sample elsewhere.
- **Exception messages.** SoapCore puts exception messages into SOAP faults by default. The sample registers `AddSoapExceptionTransformer` so only the messages of `FaultException` and `AuthenticationException` (both meant for callers: the second one is what `/Secure.svc` throws for missing or wrong credentials) reach the client, and everything else becomes a generic text.
- **`/Secure.svc` is a demo, and its GET route is closed.** SoapCore also runs `GET {path}/{Operation}?p=...` when GET is enabled, and that route skips message processors, so without a guard it would execute `Echo` with no credentials. `Program.cs` answers 404 for any path below `/Secure.svc` (`SecureDemoMessageProcessor.IsOperationRoute`). If you protect an endpoint with a message processor, do the same, or keep GET disabled on it.
