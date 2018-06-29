using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SoapCore;

namespace Server
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.TryAddSingleton<SampleService>();
			services.AddMvc();
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole();
			loggerFactory.AddDebug();

			app.UseSoapEndpoint<SampleService>("/Service.svc", new BasicHttpBinding(), SoapSerializer.DataContractSerializer);
			app.UseSoapEndpoint<SampleService>("/Service.asmx", new BasicHttpBinding(), SoapSerializer.XmlSerializer);
			app.UseMvc();
		}
	}
}
