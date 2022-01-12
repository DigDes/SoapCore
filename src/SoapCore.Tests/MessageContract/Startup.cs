using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SoapCore.Tests.MessageContract.Models;

namespace SoapCore.Tests.MessageContract
{
	public class Startup
	{
		private readonly Type _serviceType;

		public Startup(IStartupConfiguration configuration)
		{
			_serviceType = configuration.ServiceType;
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
			app.UseSoapEndpoint(_serviceType, "/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			app.UseSoapEndpoint(_serviceType, "/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);

			app.UseMvc();
		}
#else
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint(_serviceType, "/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				x.UseSoapEndpoint(_serviceType, "/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
				x.UseSoapEndpoint(_serviceType, opt =>
				{
					opt.Path = "/ServiceWithAdditionalEnvelopeXmlnsAttributes.asmx";
					opt.AdditionalEnvelopeXmlnsAttributes = new Dictionary<string, string>()
					{
						{ "arr", "http://schemas.microsoft.com/2003/10/Serialization/Arrays" }
					};
				});
			});
		}
#endif
	}
}
