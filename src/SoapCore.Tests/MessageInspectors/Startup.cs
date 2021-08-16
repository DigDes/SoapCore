using System.ServiceModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SoapCore.Tests.MessageInspectors.MessageInspector;
using SoapCore.Tests.MessageInspectors.MessageInspector2;

namespace SoapCore.Tests.MessageInspectors
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
			InspectorStyle = configuration.GetValue<InspectorStyle>("InspectorStyle");
		}

		public IConfiguration Configuration { get; }
		public InspectorStyle InspectorStyle { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSoapCore();
			services.TryAddSingleton<TestService>();

			switch (InspectorStyle)
			{
				case InspectorStyle.MessageInspector:
#pragma warning disable CS0612 // Type or member is obsolete
					services.AddSoapMessageInspector(new MessageInspectorMock());
#pragma warning restore CS0612 // Type or member is obsolete
					break;
				case InspectorStyle.MessageInspector2:
					services.AddSoapMessageInspector(new MessageInspector2Mock());
					break;
			}

			services.AddMvc();
		}

#if !NETCOREAPP3_0_OR_GREATER
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			app.UseMvc();
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
