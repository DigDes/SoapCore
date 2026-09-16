using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SoapCore.Tests.MessageInspectors;
using SoapCore.Tests.MessageInspectors.MessageInspector2;

namespace SoapCore.Tests.Serialization.Issue1194;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
		InspectorStyle = configuration.GetValue<InspectorStyle>("InspectorStyle");
	}

	public IConfiguration Configuration { get; }
	public InspectorStyle InspectorStyle { get; }

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddSoapCore();
		services.TryAddSingleton<TestService>();
		services.AddSoapMessageInspector(new MessageInspector2MockNoException());

		switch (InspectorStyle)
		{
			case InspectorStyle.MessageInspector2:
				services.AddSoapMessageInspector(new MessageInspector2Mock());

				break;
			case InspectorStyle.MessageInspector2NoException:
				break;
		}

		services.AddMvc();
	}

#if !NETCOREAPP3_0_OR_GREATER
	public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
	{
		var soapEncoderOptions = CreateSoapEncoderOptions();
		app.UseSoapEndpoint<TestService>("/Service.svc", soapEncoderOptions, SoapSerializer.XmlSerializer, true, indentXml: true, omitXmlDeclaration: true);
		app.UseMvc();
	}
#else
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
	{
		app.UseRouting();

		var soapEncoderOptions = CreateSoapEncoderOptions();
		app.UseSoapEndpoint<TestService>("/Service.svc", soapEncoderOptions, SoapSerializer.XmlSerializer, true, indentXml: true, omitXmlDeclaration: true);
	}
#endif

	private static SoapEncoderOptions[] CreateSoapEncoderOptions()
	{
		return
		[
			new SoapEncoderOptions
			{
				MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None),
				WriteEncoding = Encoding.UTF8,
				ReaderQuotas = XmlDictionaryReaderQuotas.Max,
				BindingName = "Soap12",
				PortName = "Soap12",
			},
			new SoapEncoderOptions
			{
				MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap11, AddressingVersion.None),
				WriteEncoding = Encoding.UTF8,
				ReaderQuotas = XmlDictionaryReaderQuotas.Max,
				BindingName = "Soap11",
				PortName = "Soap11",
			}

		];
	}
}
