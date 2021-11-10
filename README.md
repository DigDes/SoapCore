# SoapCore

[![NuGet Version](https://img.shields.io/nuget/v/SoapCore.svg)](https://www.nuget.org/packages/SoapCore/) ![](https://github.com/DigDes/SoapCore/workflows/CI/badge.svg) [![Stack Overflow](https://img.shields.io/badge/stackoverflow-questions-blue?logo=stackoverflow)](https://stackoverflow.com/questions/tagged/soapcore)

SOAP protocol middleware for ASP.NET Core

Based on Microsoft article: [Custom ASP.NET Core Middleware Example](https://blogs.msdn.microsoft.com/dotnet/2016/09/19/custom-asp-net-core-middleware-example/).

Support ref\out params, exceptions. Works with legacy SOAP\WCF-clients.

## Getting Started

### Requirements

The following frameworks are supported:

- .NET 6.0 (using ASP.NET Core 6.0)
- .NET 5.0 (using ASP.NET Core 5.0)
- .NET Core 3.1 (using ASP.NET Core 3.1)
- .NET Standard 2.0 (using ASP.NET Core 2.1)
- .NET Standard 2.1 (using ASP.NET Core 2.2)

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
        endpoints.UseSoapEndpoint<ServiceContractImpl>("/ServicePath.asmx", new SoapEncoderOptions());
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

### Using with legacy WCF/WS

It is possible to use SoapCore with .NET legacy WCF and Web Services, both as client and service.

Primary point here is to use XmlSerializer and properly markup messages and operations with xml serialization attributes. You may use legacy pre-generated wrappers to obtain these contracts or implement them manually. Extended example is available under serialization tests project.

### Using with external WSDL / XSD schemas

There is an optional feature included where you can instead of generating service description from code get the service description from files stored on the server.

To use it, add a setting like this to appsettings

```csharp
 "FileWSDL": {
    "UrlOverride": "",
    "WebServiceWSDLMapping": {
      "Service.asmx": {
        "WsdlFile": "snapshotpull.wsdl",
        "SchemaFolder": "Schemas",
        "WsdlFolder": "Schemas"
      }
    },
    "VirtualPath": ""
```

* UrlOverride - can be used to override the URL in the service description. This can be useful if you are behind a firewall.
* Service.asmx - is the endpoint of the service you expose. You can have more than one.
* WsdlFile - is the name of the WSDL on disc.
* SchemaFolder - if you import XSD from WSDL, this is the folder where the Schemas are stored on disc.
* WsdlFolder - is the folder that the WSDL file is stored on disc.
* VirualPath - can be used if you like to add a path between the base URL and service.

To read the setting you can do the following

In Startup.cs:


```csharp

var settings = Configuration.GetSection("FileWSDL").Get<WsdlFileOptions>();
settings.AppPath = env.ContentRootPath; // The hosting environment root path
...

app.UseSoapEndpoint<ServiceContractImpl>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, false, null, settings);
```

If the WsdFileOptions parameter is supplied then this feature is enabled / used.

### References

* [stackify.com/soap-net-core](https://stackify.com/soap-net-core/)

### Tips and Tricks

#### Extending the pipeline

In your ConfigureServices method, you can register some additional items to extend the pipeline:
* services.AddSoapMessageInspector() - add a custom MessageInspector. This function is similar to the `IDispatchMessageInspector` in WCF. The newer `IMessageInspector2` interface allows you to register multiple inspectors, and to know which service was being called.
* services.AddSingleton<MyOperatorInvoker>() - add a custom OperationInvoker. Similar to WCF's `IOperationInvoker` this allows you to override the invoking of a service operation, commonly to add custom logging or exception handling logic around it.

#### How to get custom HTTP header in SoapCore service

Use interface IServiceOperationTuner to tune each operation call.

Create class that implements IServiceOperationTuner.
Parameters in Tune method:
* httpContext - current HttpContext. Can be used to get http headers or body.
* serviceInstance - instance of your service.
* operation - information about called operation.

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
### Contributing

See [Contributing guide](CONTRIBUTING.md)

### Contributors
<a href="https://github.com/digdes/soapcore/graphs/contributors">
  <img src="https://contributors-img.web.app/image?repo=digdes/soapcore" />
</a>

Made with [contributors-img](https://contributors-img.web.app).
