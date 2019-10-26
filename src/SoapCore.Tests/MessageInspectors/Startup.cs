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
					services.AddSoapMessageInspector(new MessageInspectorMock());
					break;
				case InspectorStyle.MessageInspector2:
					services.AddSoapMessageInspector(new MessageInspector2Mock());
					break;
			}

			services.AddMvc();
		}

#if ASPNET_21
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseSoapEndpoint<TestService>("/Service.svc", new BasicHttpBinding(), SoapSerializer.DataContractSerializer);
			app.UseMvc();
		}
#endif
#if ASPNET_30
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint<TestService>("/Service.svc", new BasicHttpBinding(), SoapSerializer.DataContractSerializer);
			});
		}
#endif
	}
}
