# SoapCore

SOAP protocol middleware for ASP.NET Core.

Based on Microsoft article: [Custom ASP.NET Core Middleware Example](https://blogs.msdn.microsoft.com/dotnet/2016/09/19/custom-asp-net-core-middleware-example/).

Support ref\out params, exceptions. Works with legacy SOAP\WCF-clients.

## Getting Started

### Installing

`PM> Install-Package SoapCore`

In Startup.cs:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSoapCore();
    services.TryAddSingleton<ServiceContractImpl>();
}
public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
    app.UseSoapEndpoint<ServiceContractImpl>("/ServicePath.asmx", new BasicHttpBinding());
}
```

### Using with legacy WCF/WS

It is possible to use SoapCore with .net legacy WCF and Web Services, both as client and service.

Primary point here is to use XmlSerializer and properly markup messages and operations with xml serialization attributes. You may use legacy pre-generated wrappers to obtain these contracts or implement them manualy. Extended example is available under serialization tests project.

### References

* [stackify.com/soap-net-core](https://stackify.com/soap-net-core/)

### Tips and Tricks

#### Extending the pipeline

In your ConfigureServices method, you can register some additional items to extend the pipeline:
* services.AddSoapMessageInspector() - add a custom MessageInspector. These function similarly to the `IDispatchMessageInspector` in WCF. The newer `IMessageInspector2` interface allows you to register multiple inspectors, and to know which service was being called.
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
    public void Tune(HttpContext httpContext, object serviceInstance, SoapCore.OperationDescription operation)
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

Register MyServiceOperationTunre in Startup class:

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

Change your service to get possibility to store information from http header:

```csharp
public class MyService : IMyServiceService
{
    // Use ThreadLocal or some of thread sinchronization stuff if service registered as singleton.
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

[![Build Status](https://travis-ci.com/DigDes/SoapCore.svg?branch=master)](https://travis-ci.com/DigDes/SoapCore)
