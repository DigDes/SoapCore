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

namespace SoapCore.Tests.WsdlFromFile
{
	public class Startup
	{
		private readonly Type _serviceType;
		private readonly string _wsdlFile;

		public Startup(IStartupConfiguration configuration)
		{
			_serviceType = configuration.ServiceType;
			_wsdlFile = configuration.WsdlFile;
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
			WsdlFileOptions options = new WsdlFileOptions
			{
				UrlOverride = string.Empty,
				VirtualPath = string.Empty,
				WebServiceWSDLMapping = new Dictionary<string, WebServiceWSDLMapping>
				{
					{
						"Service.asmx", new WebServiceWSDLMapping
						{
							SchemaFolder = "/WsdlFromFile/WSDL",
							WsdlFile = _wsdlFile,
							WSDLFolder = "/WsdlFromFile/WSDL",
							UrlOverride = "Management/Service.asmx"
						}
					}
				},
				AppPath = env.ContentRootPath
			};

			app.UseSoapEndpoint(_serviceType, "/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			app.UseSoapEndpoint(_serviceType, "/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, false, null, options);

			app.UseMvc();
		}
#else
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			WsdlFileOptions options = new WsdlFileOptions
			{
				UrlOverride = string.Empty,
				VirtualPath = string.Empty,
				WebServiceWSDLMapping = new Dictionary<string, WebServiceWSDLMapping>
				{
					{
						"Service.asmx", new WebServiceWSDLMapping
						{
							SchemaFolder = "/WsdlFromFile/WSDL",
							WsdlFile = _wsdlFile,
							WSDLFolder = "/WsdlFromFile/WSDL",
							UrlOverride = "Management/Service.asmx"
						}
					}
				},
				AppPath = env.ContentRootPath
			};

			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint(_serviceType, "/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				x.UseSoapEndpoint(_serviceType, "/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, false, null, options);
			});
		}
#endif
	}
}
