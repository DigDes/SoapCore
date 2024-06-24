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
		private readonly string _serviceName;
		private readonly Type _serviceType;
		private readonly string _testFileFolder;
		private readonly string _wsdlFile;

		public Startup(IStartupConfiguration configuration)
		{
			_serviceName = configuration.ServiceName;
			_serviceType = configuration.ServiceType;
			_testFileFolder = configuration.TestFileFolder;
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
						_serviceName + ".asmx", new WebServiceWSDLMapping
						{
							SchemaFolder = "/WsdlFromFile/" + _testFileFolder,
							WsdlFile = _wsdlFile,
							WSDLFolder = "/WsdlFromFile/" + _testFileFolder,
							UrlOverride = "Management/" + _serviceName + ".asmx"
						}
					}
				},
				AppPath = env.ContentRootPath
			};

			app.UseSoapEndpoint(_serviceType, "/" + _serviceName + ".svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			app.UseSoapEndpoint(_serviceType, "/" + _serviceName + ".asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, false, null, options);

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
						_serviceName + ".asmx", new WebServiceWSDLMapping
						{
							SchemaFolder = "/WsdlFromFile/" + _testFileFolder,
							WsdlFile = _wsdlFile,
							WSDLFolder = "/WsdlFromFile/" + _testFileFolder,
							UrlOverride = "Management/" + _serviceName + ".asmx"
						}
					}
				},
				AppPath = env.ContentRootPath
			};

			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint(_serviceType, "/" + _serviceName + ".svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				x.UseSoapEndpoint(_serviceType, "/" + _serviceName + ".asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer, false, null, options);
			});
		}
#endif
	}
}
