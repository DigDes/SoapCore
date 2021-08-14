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
							WsdlFile = "SnapshotPull.wsdl",
							WSDLFolder = "/WsdlFromFile/WSDL"
						}
					}
				},
				AppPath = env.ContentRootPath
			};

			app.UseSoapEndpoint(_serviceType, "/Service.svc", SoapEncoderOptions.Default(), SoapSerializer.DataContractSerializer);
			app.UseSoapEndpoint(_serviceType, "/Service.asmx", SoapEncoderOptions.Default(), SoapSerializer.XmlSerializer, false, null, options);

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
							WsdlFile = "SnapshotPull.wsdl",
							WSDLFolder = "/WsdlFromFile/WSDL"
						}
					}
				},
				AppPath = env.ContentRootPath
			};

			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint(_serviceType, "/Service.svc", SoapEncoderOptions.Default(), SoapSerializer.DataContractSerializer);
				x.UseSoapEndpoint(_serviceType, "/Service.asmx", SoapEncoderOptions.Default(), SoapSerializer.XmlSerializer, false, null, options);
			});
		}
#endif
	}
}
