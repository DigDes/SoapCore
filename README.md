# SoapCore

[![NuGet Version](https://img.shields.io/nuget/v/SoapCore.svg)](https://www.nuget.org/packages/SoapCore/) ![](https://github.com/DigDes/SoapCore/workflows/CI/badge.svg) [![Stack Overflow](https://img.shields.io/badge/stackoverflow-questions-blue?logo=stackoverflow)](https://stackoverflow.com/questions/tagged/soapcore)

SOAP protocol middleware for ASP.NET Core

Based on Microsoft article: [Custom ASP.NET Core Middleware Example](https://blogs.msdn.microsoft.com/dotnet/2016/09/19/custom-asp-net-core-middleware-example/).

Support ref\out params, exceptions. Works with legacy SOAP\WCF-clients.

## Getting Started

### Requirements

The following frameworks are supported:

- .NET 8.0 (using ASP.NET Core 8.0)
- .NET Core 3.1 (using ASP.NET Core 3.1)
- .NET Standard 2.0-2.1 (using ASP.NET Core 2.1)

### Installing

`PM> Install-Package SoapCore`

There are 2 different ways of adding SoapCore to your ASP.NET Core website. If you are using ASP.NET Core 3.1 or higher with endpoint routing enabled (the default):

In Startup.cs:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSoapCore();
    services.TryAddSingleton<ServiceContractImpl>();
    services.AddMvc();
}

public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
    app.UseRouting();

    app.UseEndpoints(endpoints => {
        endpoints.UseSoapEndpoint<ServiceContractImpl>(opt =>
	{
		opt.Path = "/ServicePath.asmx",
		opt.SoapSerializer = SoapSerializer.DataContractSerializer
	});
    });
    
}
```

If you are using ASP.NET Core 2.1 (i.e., on .NET Framework, .NET Core 2.1, or another .NET Standard 2.0 compliant platform):

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSoapCore();
    services.TryAddSingleton<ServiceContractImpl>();
    services.AddMvc();
}
public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
    app.UseSoapEndpoint<ServiceContractImpl>("/ServicePath.asmx", new SoapEncoderOptions());
}
```

### Using with custom implementation of Serialization

There is an optional feature included where you can implment the ISoapCoreSerializer to built your own custom serializar for body.

In Startup.cs:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddSoapCore();
    services.TryAddSingleton<ServiceContractImpl>();
    services.AddCustomSoapMessageSerializer<CustomeBodyMessageSerializerImpl>();  //Add Your Custom Implementation or Extend Default Serializer

    services.AddMvc();
    ...
}

public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
    app.UseSoapEndpoint<ServiceContractImpl>(soapCoreOptions =>
    {
        soapCoreOptions.Path = "/ServicePath.asmx";
        soapCoreOptions.UseCustomSerializer<CustomeBodyMessageSerializerImpl>();  //Specify the Service to Use Service Soap Message Serializer
        soapCoreOptions.SoapSerializer = SoapSerializer.DataContractSerializer;
        ...
    });
}

```

### Using with legacy WCF/WS

It is possible to use SoapCore with .NET legacy WCF and Web Services, both as client and service.

Primary point here is to use XmlSerializer and properly markup messages and operations with xml serialization attributes. You may use legacy pre-generated wrappers to obtain these contracts or implement them manually. Extended example is available under serialization tests project.

### Using with external WSDL / XSD schemas

There is an optional feature included where you can instead of generating service description from code get the service description from files stored on the server.

To use it, add a setting like this to appsettings

```json
"FileWSDL": {
  "UrlOverride": "",
  "SchemeOverride": "",
  "VirtualPath": "",
  "WebServiceWSDLMapping": {
    "Service.asmx": {
      "UrlOverride": "Management/Service.asmx",
      "WsdlFile": "snapshotpull.wsdl",
      "SchemaFolder": "Schemas",
      "WsdlFolder": "Schemas"
    }
  }
}
```

* UrlOverride - can be used to override the URL in the service description. This can be useful if you are behind a firewall.
* SchemeOverride - can be used to override the HTTP Scheme in the service description. This can be useful if you are behind a firewall and the firewall sets the X-Forwarded-Host header, but the internal HTTP scheme is not the same as the external.
* VirtualPath - can be used if you like to add a path between the base URL and service. 
* WebServiceWSDLMapping
  * UrlOverride - can be used to override the URL for a specific WSDL mapping. This can be useful if you want to host different services under different folder.
  * Service.asmx - is the endpoint of the service you expose. You can have more than one.
  * WsdlFile - is the name of the WSDL on disc.
  * SchemaFolder - if you import XSD from WSDL, this is the folder where the Schemas are stored on disc.
  * WsdlFolder - is the folder that the WSDL file is stored on disc.


To read the setting you can do the following

In Startup.cs:

```csharp
var settings = Configuration.GetSection("FileWSDL").Get<WsdlFileOptions>();

// For case-insensitive mapping, if you are using "SoapCoreOptions.CaseInsensitivePath = true" - otherwise URLs with different casing won't be mapped correctly
//var settings = Configuration.GetSection("FileWSDL").Get<WsdlFileOptionsCaseInsensitive>();

settings.AppPath = env.ContentRootPath; // The hosting environment root path
...

app.UseSoapEndpoint<ServiceContractImpl>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, false, null, settings);
```

If the WsdFileOptions parameter is supplied then this feature is enabled / used.

### References

- [stackify.com/soap-net-core](https://stackify.com/soap-net-core/)

### Tips and Tricks

#### Extending the pipeline

In your ConfigureServices method, you can register some additional items to extend the pipeline:

- services.AddSoapMessageInspector() - add a custom MessageInspector. This function is similar to the `IDispatchMessageInspector` in WCF. The newer `IMessageInspector2` interface allows you to register multiple inspectors, and to know which service was being called.
- services.AddSingleton<MyOperatorInvoker>() - add a custom OperationInvoker. Similar to WCF's `IOperationInvoker` this allows you to override the invoking of a service operation, commonly to add custom logging or exception handling logic around it.
- services.AddSoapMessageProcessor() - add a custom SoapMessageProcessor. Similar to ASP.NET Cores middlewares, this allows you to inspect the message on the way in and out. You can also short-circuit the message processing and return your own custom message instead. Inspecting and modifying HttpContext is also possible

#### Using ISoapMessageProcessor()

```csharp
//Add this to ConfigureServices in Startup.cs

services.AddSoapMessageProcessor(async (message, httpcontext, next) =>
{
	var bufferedMessage = message.CreateBufferedCopy(int.MaxValue);
	var msg = bufferedMessage.CreateMessage();
	var reader = msg.GetReaderAtBodyContents();
	var content = reader.ReadInnerXml();

	//now you can inspect and modify the content at will.
	//if you want to pass on the original message, use bufferedMessage.CreateMessage(); otherwise use one of the overloads of Message.CreateMessage() to create a new message
	var originalMessage = bufferedMessage.CreateMessage();

	//pass the modified message on to the rest of the pipe.
	var responseMessage = await next(message);

	//Inspect and modify the contents of returnMessage in the same way as the incoming message.
	//finish by returning the modified message.

	return responseMessage;
});
```

#### How to get custom HTTP header in SoapCore service

Use interface IServiceOperationTuner to tune each operation call.

Create class that implements IServiceOperationTuner.
Parameters in Tune method:

- httpContext - current HttpContext. Can be used to get http headers or body.
- serviceInstance - instance of your service.
- operation - information about called operation.

```csharp
public class MyServiceOperationTuner : IServiceOperationTuner
{
    public void Tune(HttpContext httpContext, object serviceInstance, SoapCore.ServiceModel.OperationDescription operation)
    {
        if (operation.Name.Equals("SomeOperationName"))
        {
            MyService service = serviceInstance as MyService;
            string result = string.Empty;

            StringValues paramValue;
            if (httpContext.Request.Headers.TryGetValue("some_parameter", out paramValue))
            {
                result = paramValue[0];
            }

            service.SetParameterForSomeOperation(result);
        }
    }
}
```

Register MyServiceOperationTuner in Startup class:

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // ...
        services.AddSoapServiceOperationTuner(new MyServiceOperationTuner());
        //...
    }
    // ...
}
```

Change your service to get the possibility to store information from http headers:

```csharp
public class MyService : IMyServiceService
{
    // Use ThreadLocal or some of thread synchronization stuff if service registered as singleton.
    private ThreadLocal<string> _paramValue = new ThreadLocal<string>() { Value = string.Empty };

    // ...

    public void SetParameterForSomeOperation(string paramValue)
    {
        _paramValue.Value = paramValue;
    }

    public string SomeOperationName()
    {
        return "Param value from http header: " + _paramValue.Value;
    }
}
```

#### Additional namespace declaration attributes in envelope

Adding additional namespaces to the **SOAP Envelope** can be done by populating `SoapEncoderOptions.AdditionalEnvelopeXmlnsAttributes` parameter.

```csharp
....
endpoints.UseSoapEndpoint<IService>(opt =>
{
	opt.Path = "/ServiceWithAdditionalEnvelopeXmlnsAttributes.asmx";
	opt.AdditionalEnvelopeXmlnsAttributes = new Dictionary<string, string>()
	{
		{ "myNS", "http://schemas.someting.org" },
		{ "arr", "http://schemas.microsoft.com/2003/10/Serialization/Arrays" }
	};
});
...
```

This code will put `xmlns:myNS="...` and `xmlns:arr="...` attributes in `Envelope` and message will look like:

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" ... xmlns:myNS="http://schemas.someting.org" xmlns:arr="http://schemas.microsoft.com/2003/10/Serialization/Arrays">
...
    <myNS:StringList>
        <arr:string>Error: one</arr:string>
        <arr:string>Error: two</arr:string>
    </fin:StringList>
...
```

instead of:

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" ... >
...
    <d3p1:StringList xmlns:d4p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays">
        <d4p1:string>Error: one</arr:string>
        <d4p1:string>Error: two</arr:string>
    </d3p1:StringList>
...
```

#### Discovering SOAP endpoints with ISoapEndpointRegistry

ASP.NET Core's `EndpointDataSource` only shows SoapCore's base route (display name `SoapCore`) and carries no service type, serializer or SOAP version. Endpoints added with `app.UseSoapEndpoint` (middleware mode) do not appear there at all.
`ISoapEndpointRegistry` is an opt-in, SOAP-aware list of every endpoint configured with `UseSoapEndpoint`, in both middleware and endpoint-routing mode. It is useful for service catalogs, dashboards, WSDL aggregation, gateways and per-endpoint observability.
When no registry is registered, SoapCore behaves exactly as before.

Enable it in `ConfigureServices`:

```csharp
services.AddSoapCore();
services.AddSoapEndpointRegistry(); // default thread-safe, in-memory registry

// Or register your own implementation instead (it must be a singleton):
// services.AddSingleton<ISoapEndpointRegistry, MyRegistry>();
```

Read it, for example to expose a JSON catalog. This sample works on every supported target framework (on .NET Standard targets, System.Text.Json comes with SoapCore):

```csharp
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text.Json;
using SoapCore;
using SoapCore.Extensibility;

public void Configure(IApplicationBuilder app)
{
	app.UseSoapEndpoint<ServiceContractImpl>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
	app.UseSoapEndpoint<ServiceContractImpl>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);

	app.Map("/soap-endpoints", catalog => catalog.Run(async context =>
	{
		var registry = context.RequestServices.GetRequiredService<ISoapEndpointRegistry>();
		var endpoints = registry.GetAll().Select(e => new
		{
			e.Path,
			Service = e.ServiceType.FullName, // System.Type is not JSON-serializable: project it
			Serializer = e.Serializer.ToString(),
			SoapVersions = e.MessageVersions.Select(v =>
				v.Envelope == EnvelopeVersion.Soap12 ? "1.2" :
				v.Envelope == EnvelopeVersion.Soap11 ? "1.1" : "none"),
		});

		context.Response.ContentType = "application/json";
		await context.Response.WriteAsync(JsonSerializer.Serialize(endpoints));
	}));
}
```

To list operations with the same SOAPAction values the endpoint uses, build a `ServiceDescription` from the recorded values. It reflects over the service assembly, so build it once and cache it:

```csharp
using SoapCore.ServiceModel;

var description = new ServiceDescription(endpoint.ServiceType, endpoint.GenerateSoapActionWithoutContractName);
foreach (var operation in description.Operations)
{
	Console.WriteLine($"{endpoint.Path} {operation.Name} {operation.SoapAction}");
}
```

Recorded values (`SoapEndpointInfo`):

| Property | Description |
|---|---|
| `Path` | Path exactly as configured (not normalized) |
| `ServiceType` | Service type passed to `UseSoapEndpoint`. Not unique: the same type can be hosted at several paths |
| `MessageType` | `T_MESSAGE` used by the endpoint (`CustomMessage` by default) |
| `Serializer` | `DataContractSerializer` or `XmlSerializer` (WSDL writer and default message serializer) |
| `CustomSerializerType` | Handler type set with `UseCustomSerializer<T>()`, or null |
| `CaseInsensitivePath` | Whether the path is matched case-insensitively |
| `GenerateSoapActionWithoutContractName` | Affects generated SOAPAction values |
| `MessageVersions` | Distinct SOAP / WS-Addressing versions of the configured encoders (SOAP 1.1 when none are configured) |

Notes:

* The registry must be a **singleton**: it is resolved from the root service provider while the pipeline is configured.
* One entry is recorded per `UseSoapEndpoint` call, in call order (endpoint routing maps four routes per call but records one entry). Endpoints with the same path in different `UseWhen` branches are recorded separately.
* Entries are a snapshot of the configuration at the time `UseSoapEndpoint` is called. Later changes to the options objects are not reflected.
* Entries are recorded while the pipeline is configured. Read the registry in a request or after `IHostApplicationLifetime.ApplicationStarted` (`IApplicationLifetime` on ASP.NET Core 2.x). An `IHostedService` may start before the pipeline is configured.
* `Path` does not include prefixes added around the endpoint (`app.Map(...)`, `UsePathBase(...)`). Add them yourself. The current request's `PathBase` is only the right prefix when the catalog and the SOAP endpoint share the same base.
* `UseSoapEndpoint` inside a route group (`MapGroup`) is not supported: SoapCore compares the full request path with the configured `Path`, so the group prefix prevents a match. Such an endpoint is still recorded, with the configured, unprefixed path.
* In middleware mode the endpoint is validated when the host builds the pipeline. An invalid configuration is recorded and then makes the application fail to start.
* `TrailingServicePathTuner` rewrites paths per request and is not reflected in the registry.
* A `SoapEndpointMiddleware` you construct yourself is not recorded. Call `Register` yourself if you need it listed.

#### Exploring SOAP services with SoapCore.UI

`SoapCore.UI` is a separate NuGet package that adds a Swagger-like explorer page to your application. It lists the endpoints known to `ISoapEndpointRegistry`, shows their operations and descriptions, generates sample SOAP envelopes from the WSDL (SOAP 1.1 and 1.2) that you edit as XML or in a typed form, and lets you authorize (bearer token, Basic, API key, WS-Security UsernameToken) and call the endpoints from the browser ("Try it out"). It has no server-side proxy and makes no external requests.

```
dotnet add package SoapCore.UI --prerelease
```

It needs `net8.0`, endpoint routing (`UseRouting` plus `UseEndpoints`, or `WebApplication`) and SoapCore 1.2.1.17 or later, the first release that has `ISoapEndpointRegistry`.

```csharp
using SoapCore;
using SoapCore.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSoapCore();
builder.Services.AddSingleton<MyService>();
builder.Services.AddSoapCoreUi(options => options.Title = "My services");

var app = builder.Build();

// WebApplication is both an IApplicationBuilder and an IEndpointRouteBuilder, so UseSoapEndpoint is ambiguous: cast to pick one.
((IEndpointRouteBuilder)app).UseSoapEndpoint<MyService>(options =>
{
	options.Path = "/Service.svc";
	options.SoapSerializer = SoapSerializer.DataContractSerializer;
});

// The explorer lists and calls your endpoints: map it in Development only, or protect it.
if (app.Environment.IsDevelopment())
{
	app.MapSoapCoreUi();   // /soap-ui
}

app.Run();
```

Outside Development, protect the explorer with the standard conventions on the builder that `MapSoapCoreUi` returns, for example `app.MapSoapCoreUi().RequireAuthorization("SoapUiPolicy")`. They cover the explorer routes only, not your SOAP endpoints. Mapping the explorer also needs `HttpGetEnabled` on the SOAP endpoints (it is `true` by default), and in SoapCore that setting lets `GET {path}/{Operation}` invoke operations, so in production set `HttpGetEnabled = false` and `HttpPostEnabled = false` and do not map the explorer.

The options, the Authorize panel, descriptions, the form view, the full security model, the limitations and troubleshooting are in the [SoapCore.UI package README](src/SoapCore.UI/README.md). A runnable sample with six endpoints, one of them protected to try the Authorize panel, is in [samples/ServerWithUi](samples/ServerWithUi).

### Not implemented
WCF and legacy WebService supports many scenarios and lots of attributes. SoapCore only supports the most common patterns.
Stuff that are not supported includes:
* XmlIncludeAttribute/SoapIncludeAttribute
* SoapDocumentMethodAttribute
* 

### Contributing

See [Contributing guide](CONTRIBUTING.md)

### Contributors

<a href="https://github.com/digdes/soapcore/graphs/contributors">
  <img src="https://contributors-img.web.app/image?repo=digdes/soapcore" />
</a>

Made with [contributors-img](https://contributors-img.web.app).
