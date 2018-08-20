using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace SoapCore.Tests.Serialization
{
	public class ServiceFixture<IService> : IDisposable where IService : class
	{
		public const int Port = 5060;
		public Mock<IService> serviceMock { get; private set; }
		public readonly IService serviceClientXml;
		public readonly IService serviceClientDC;
		private readonly IWebHost host;

		public static IEnumerable<object[]> SoapSerializersList()
		{
			foreach (var soapSerializer in Enum.GetValues(typeof(SoapSerializer)))
			{
				yield return new object[] { soapSerializer };
			}
		}

		private readonly Dictionary<SoapSerializer, IService> sampleServiceClients
			= new Dictionary<SoapSerializer, IService>();

		public IService GetSampleServiceClient(SoapSerializer soapSerializer)
		{
			WaitForServerStarted();
			return sampleServiceClients[soapSerializer];
		}

		public ServiceFixture()
		{
			// start service host

			this.host = new WebHostBuilder()
				.ConfigureServices(services =>
				{
					// init SampleService service mock
					this.serviceMock = new Mock<IService>();
					services.AddSingleton<IService>(serviceMock.Object);
					services.AddMvc();
				})
				.Configure(appBuilder =>
				{
					appBuilder.UseSoapEndpoint<IService>("/Service.svc", new BasicHttpBinding(), SoapSerializer.DataContractSerializer);
					appBuilder.UseSoapEndpoint<IService>("/Service.asmx", new BasicHttpBinding(), SoapSerializer.XmlSerializer);
					appBuilder.UseMvc();
				})
				.UseKestrel()
				.UseUrls($"http://*:{Port}")
				.UseContentRoot(Directory.GetCurrentDirectory())
				.Build();

#pragma warning disable 4014
			host.RunAsync();
#pragma warning restore 4014

			// make service client

			var binding = new BasicHttpBinding();

			var endpointXml = new EndpointAddress(new Uri($"http://localhost:{Port}/Service.asmx"));
			var channelFactoryXml = new ChannelFactory<IService>(binding, endpointXml);
			this.serviceClientXml = channelFactoryXml.CreateChannel();

			var endpointDC = new EndpointAddress(new Uri($"http://localhost:{Port}/Service.svc"));
			var channelFactoryDC = new ChannelFactory<IService>(binding, endpointDC);
			this.serviceClientDC = channelFactoryDC.CreateChannel();

			this.sampleServiceClients[SoapSerializer.XmlSerializer] = this.serviceClientXml;
			this.sampleServiceClients[SoapSerializer.DataContractSerializer] = this.serviceClientDC;
		}

		public void Dispose()
		{
			this.host.StopAsync().GetAwaiter().GetResult();
		}

		private void WaitForServerStarted()
		{
			using (var client = new TcpClient())
			{
				for (var i = 0; i < 10 && !client.Connected; i++)
					try
					{
						client.Connect("localhost", Port);
					}
					catch
					{
						Thread.Sleep(1000);
					}
			}
		}
	}
}
