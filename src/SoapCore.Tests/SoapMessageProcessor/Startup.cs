using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace SoapCore.Tests.SoapMessageProcessor
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSoapCore();
			services.TryAddSingleton<TestService>();
			services.AddSoapMessageProcessor(async (message, httpcontext, next) =>
			{
				await Task.Delay(1);

				if (httpcontext.Request.Path.Value.Contains("ServiceWithProcessor.svc"))
				{
					return Message.CreateMessage(MessageVersion.Soap11, "none");
				}
				else
				{
					return await next(message);
				}
			});
			services.AddMvc();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint<TestService>("/ServiceWithProcessor.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				x.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			});
		}
	}
}
