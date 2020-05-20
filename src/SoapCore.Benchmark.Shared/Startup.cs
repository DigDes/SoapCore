using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SoapCore;
using System.ServiceModel;

namespace SoapCore.Benchmark
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<PingService>();
		}

#if ASPNET_21
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
#endif
#if ASPNET_30
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
#endif
		{

			// var elm = new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, Encoding.UTF8);
			// var customBinding = new CustomBinding("MarWebSvcSoap", "http://intercom/malion/MarWebSvc", new BindingElement[] { elm });
			// var customBinding = new BasicHttpBinding();

			app.UseSoapEndpoint<PingService>("/TestService.asmx", new BasicHttpBinding(), SoapSerializer.DataContractSerializer);
			app.Use(async (ctx, next) =>
			{
				await ctx.Response.WriteAsync("").ConfigureAwait(false);
			});
		}
	}
}
