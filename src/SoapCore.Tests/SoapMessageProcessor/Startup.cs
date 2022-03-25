using System.IO;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace SoapCore.Tests.SoapMessageProcessor
{
	public class Startup
	{
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSoapCore();
			services.TryAddSingleton<TestService>();
			services.AddSoapMessageProcessor(async (message, httpcontext, next) =>
			{
				await Task.Delay(1);

				if (httpcontext.Request.Path.Value.Contains("ServiceWithProcessor.svc"))
				{
					return Message.CreateMessage(MessageVersion.Soap11, "none");
				}
				else if (httpcontext.Request.Path.Value.Contains("ServiceWithPongProcessor.svc"))
				{
					var msg = await next(message);
					var reader = msg.GetReaderAtBodyContents();

					var content = await reader.ReadOuterXmlAsync();

					var ms = new MemoryStream(Encoding.UTF8.GetBytes(content.Replace("Ping", "Pong")));
					var xmlReader = XmlReader.Create(ms);

					return Message.CreateMessage(msg.Version, null, xmlReader);
				}
				else
				{
					return await next(message);
				}
			});
			services.AddMvc();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			app.UseRouting();

			app.UseEndpoints(x =>
			{
				x.UseSoapEndpoint<TestService>("/ServiceWithProcessor.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				x.UseSoapEndpoint<TestService>("/ServiceWithPongProcessor.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
				x.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			});
		}
	}
}
