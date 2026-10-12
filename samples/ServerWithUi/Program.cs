using System.Security.Authentication;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Models;
using ServerWithUi;
using ServerWithUi.HeaderDemo;
using ServerWithUi.Secure;
using ServerWithUi.Weather;
using SoapCore;
using SoapCore.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSoapCore();
// SoapCore puts exception messages into SOAP faults by default. Keep the messages of FaultException and AuthenticationException (both meant
// for callers: the second one is what /Secure.svc throws when the credentials are missing or wrong) and hide the rest.
builder.Services.AddSoapExceptionTransformer(ex => (ex is FaultException || ex is AuthenticationException) ? ex.Message : "The service could not process the request.");
// DEMO ONLY: /Secure.svc accepts a bearer token, HTTP Basic, an API key or a WS-Security UsernameToken (see Secure/SecureDemoMessageProcessor.cs).
// The processor does nothing for the other endpoints.
builder.Services.AddSoapMessageProcessor(new SecureDemoMessageProcessor());
builder.Services.AddSoapCoreUi(options =>
{
	options.Title = "SoapCore sample explorer";
	options.Description = "Sample services for the SoapCore explorer. Try /Weather.svc, or /Secure.svc after pressing Authorize (the demo credentials are in the README of the sample).";
	// Opt-in: shows the parameters, return type and faults of each operation, which reveals the CLR type names of the contracts.
	options.ExposeOperationDetails = true;
	// The descriptions of the operations come from the XML documentation file of this project (see GenerateDocumentationFile in the csproj)
	// and from [Description] attributes. A missing file is only logged: the explorer then goes on without those descriptions.
	options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "ServerWithUi.xml"));
	// The header that the "API key" scheme of the Authorize panel uses. It is what /Secure.svc checks. AuthSchemes stays at its default, All.
	options.ApiKeyHeaderName = SecureDemoMessageProcessor.ApiKeyHeaderName;
});
builder.Services.TryAddSingleton<ISampleService, SampleService>();
builder.Services.TryAddSingleton<IWeatherService, WeatherService>();
builder.Services.TryAddSingleton<IHeaderDemoService, HeaderDemoService>();
builder.Services.TryAddSingleton<ISecureService, SecureService>();

var app = builder.Build();

var explorerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("SoapUi:Enabled");

// Optional: run behind a path base, for example `PathBase=/demo dotnet run` (see the README for PowerShell and Git Bash).
var pathBase = NormalizePathBase(app.Configuration["PathBase"]);
if (pathBase != null)
{
	app.UsePathBase(pathBase);
	app.UseRouting();   // explicit, so WebApplication does not insert UseRouting before UsePathBase
}

// With GET enabled (the explorer needs it for ?wsdl) SoapCore also runs GET {path}/{Operation}?p=..., and that route does not go through the
// SOAP message processors, so it would skip the credentials of /Secure.svc. Close it: only /Secure.svc itself (SOAP POST and ?wsdl) is served.
app.Use(async (context, next) =>
{
	if (SecureDemoMessageProcessor.IsOperationRoute(context.Request.Path))
	{
		context.Response.StatusCode = StatusCodes.Status404NotFound;
		return;
	}

	await next();
});

// WebApplication implements IApplicationBuilder and IEndpointRouteBuilder, so UseSoapEndpoint is ambiguous (CS0121). Cast to choose the mode.
var routes = (IEndpointRouteBuilder)app;
var pipeline = (IApplicationBuilder)app;

var soap11 = new[] { new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11 } };

routes.UseSoapEndpoint<ISampleService>(options =>
{
	options.Path = "/Service.svc";
	options.SoapSerializer = SoapSerializer.DataContractSerializer;
	options.EncoderOptions = Soap11And12(nameof(ISampleService));
	Harden(options, explorerEnabled);
});

routes.UseSoapEndpoint<ISampleService>(options =>
{
	options.Path = "/Service.asmx";
	options.SoapSerializer = SoapSerializer.XmlSerializer;
	options.EncoderOptions = soap11;
	Harden(options, explorerEnabled);
});

routes.UseSoapEndpoint<IWeatherService>(options =>
{
	options.Path = "/Weather.svc";
	options.SoapSerializer = SoapSerializer.DataContractSerializer;
	options.EncoderOptions = Soap11And12(nameof(IWeatherService));
	options.CaseInsensitivePath = true;
	Harden(options, explorerEnabled);
});

routes.UseSoapEndpoint<IHeaderDemoService>(options =>
{
	options.Path = "/NoWsdl.svc";
	options.SoapSerializer = SoapSerializer.DataContractSerializer;
	options.EncoderOptions = soap11;
	options.GenerateSoapActionWithoutContractName = true;
	Harden(options, false);   // GET disabled on purpose: the explorer falls back to a generic envelope
});

// Protected by SecureDemoMessageProcessor: the SOAP POST needs one of the demo credentials, the WSDL does not.
routes.UseSoapEndpoint<ISecureService>(options =>
{
	options.Path = SecureDemoMessageProcessor.EndpointPath;
	options.SoapSerializer = SoapSerializer.DataContractSerializer;
	options.EncoderOptions = Soap11And12(nameof(ISecureService));
	options.CaseInsensitivePath = true;
	Harden(options, explorerEnabled);
});

// Middleware mode: it never appears in EndpointDataSource, but the registry records it.
pipeline.UseSoapEndpoint<IWeatherService>(options =>
{
	options.Path = "/Legacy/Weather.asmx";
	options.SoapSerializer = SoapSerializer.XmlSerializer;
	options.EncoderOptions = soap11;
	Harden(options, explorerEnabled);
});

if (explorerEnabled)
{
	app.MapSoapCoreUi();                       // add .RequireAuthorization() to protect the explorer
	// app.MapSoapCoreUi().RequireAuthorization("SoapUiPolicy");
	// app.MapSoapCoreUi().RequireHost("localhost");
}

app.Run();

// GET serves ?wsdl, which the explorer needs. In SoapCore it ALSO enables GET {path}/{Operation}?x=..., which executes operations.
// So GET is enabled only while the explorer is. Form POST to {path}/{Operation} is always disabled; SOAP POST to {path} keeps working.
// With DataContractSerializer, SoapCore names the WSDL binding and port of every encoder after the contract, so SOAP 1.1 and SOAP 1.2
// would get the same name and tools such as dotnet-svcutil reject the WSDL ("more than one binding named ..."). Name the SOAP 1.2 one.
static SoapEncoderOptions[] Soap11And12(string contractName)
{
	var soap12Name = "Soap12Binding_" + contractName;

	return new[]
	{
		new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11 },
		new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10, BindingName = soap12Name, PortName = soap12Name },
	};
}

static void Harden(SoapCoreOptions options, bool allowGet)
{
	options.HttpGetEnabled = allowGet;
	options.HttpsGetEnabled = allowGet;
	options.HttpPostEnabled = false;
	options.HttpsPostEnabled = false;
}

static string NormalizePathBase(string value)
{
	if (string.IsNullOrWhiteSpace(value))
	{
		return null;
	}

	var trimmed = "/" + value.Trim().Trim('/');
	return System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^(/[A-Za-z0-9._~-]+)+$") ? trimmed : null;
}
