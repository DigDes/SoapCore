using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.Wsdl
{
	public class Startup
	{
		private readonly Type _serviceType;

		private readonly string _schemeOverride;

		public Startup(IStartupConfiguration configuration)
		{
			_serviceType = configuration.ServiceType;
			_schemeOverride = configuration.SchemeOverride;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSoapCore();
			services.TryAddSingleton(_serviceType);
			services.AddMvc();
		}

#if !NETCOREAPP3_0_OR_GREATER
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseSoapEndpoint(_serviceType, "/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer, schemeOverride: _schemeOverride);
			app.UseSoapEndpoint(_serviceType, "/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, schemeOverride: _schemeOverride);

			app.UseMvc();
		}
#else
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint(_serviceType, "/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer, schemeOverride: _schemeOverride);
				x.UseSoapEndpoint(_serviceType, "/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, schemeOverride: _schemeOverride);
			});
		}
#endif
	}
}
