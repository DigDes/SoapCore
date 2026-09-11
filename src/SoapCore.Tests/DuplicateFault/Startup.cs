using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SoapCore.Extensibility;

namespace SoapCore.Tests.DuplicateFault
{
	public class Startup
	{
		public const string ThrowingResponseFilterSetting = "RegisterThrowingResponseFilter";

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
			RegisterThrowingResponseFilter = configuration.GetValue<bool>(ThrowingResponseFilterSetting);
		}

		public IConfiguration Configuration { get; }

		public bool RegisterThrowingResponseFilter { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			var loggerProvider = new CountingLoggerProvider();
			var faultExceptionTransformer = new CountingFaultExceptionTransformer();
			var messageInspector = new StampingMessageInspector();

			services.AddSingleton(loggerProvider);
			services.AddLogging(logging => logging.AddProvider(loggerProvider));

			services.AddSoapCore();
			services.TryAddSingleton<TestService>();

			services.AddSingleton(faultExceptionTransformer);
			services.AddSingleton<IFaultExceptionTransformer>(faultExceptionTransformer);

			services.AddSingleton(messageInspector);
			services.AddSoapMessageInspector(messageInspector);

			if (RegisterThrowingResponseFilter)
			{
				services.AddSoapMessageFilter(new ThrowingResponseFilter());
			}

			services.AddRouting();
		}

#if !NETCOREAPP3_0_OR_GREATER
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
		}
#else
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			});
		}
#endif
	}
}
