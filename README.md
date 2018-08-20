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

### Contributing

See [Contributin guide](CONTRIBUTING.md)

[![Build Status](https://travis-ci.com/DigDes/SoapCore.svg?branch=master)](https://travis-ci.com/DigDes/SoapCore)
