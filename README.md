[![Build Status](https://travis-ci.com/DigDes/SoapCore.svg?branch=master)](https://travis-ci.com/DigDes/SoapCore)

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

### References

* [stackify.com/soap-net-core](https://stackify.com/soap-net-core/)
