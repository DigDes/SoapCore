using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace SoapCore.Tests
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.TryAddSingleton<TestService>();
			services.AddMvc();
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseSoapEndpoint<TestService>("/Service.svc", new BasicHttpBinding(), SoapSerializer.DataContractSerializer);
			var transportBinding = new HttpTransportBindingElement();
			var textEncodingBinding = new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, System.Text.Encoding.UTF8);
			app.UseSoapEndpoint<TestService>("/ServiceSoap12.svc", new CustomBinding(transportBinding, textEncodingBinding), SoapSerializer.DataContractSerializer);
			app.UseMvc();
		}
	}
}
