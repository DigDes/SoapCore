using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.ServiceModel.Channels;
using System.ServiceModel;
using SoapCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Text;

namespace SoapCore.Benchmark
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<PingService>();
		}
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

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
