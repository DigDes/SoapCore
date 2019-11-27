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
	public class ServiceFixture<IService> : IDisposable
		where IService : class
	{
		public const int Port = 5060;

		private readonly IWebHost _host;
		private readonly Dictionary<SoapSerializer, IService> _sampleServiceClients = new Dictionary<SoapSerializer, IService>();
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		public ServiceFixture()
		{
			// start service host
			_host = new WebHostBuilder()
				.ConfigureServices(services =>
				{
					// init SampleService service mock
					ServiceMock = new Mock<IService>();
					services.AddSingleton(ServiceMock.Object);
					services.AddSoapCore();
					services.AddMvc();
				})
				.Configure(appBuilder =>
				{
					appBuilder.UseSoapEndpoint<IService>("/Service.svc", new BasicHttpBinding(), SoapSerializer.DataContractSerializer);
					appBuilder.UseSoapEndpoint<IService>("/Service.asmx", new BasicHttpBinding(), SoapSerializer.XmlSerializer);
					appBuilder.UseSoapEndpoint<IService>("/Service.wsdl", new BasicHttpBinding(), SoapSerializer.XmlWcfSerializer);
					appBuilder.UseMvc();
				})
				.UseKestrel()
				.UseUrls($"http://*:{Port}")
				.UseContentRoot(Directory.GetCurrentDirectory())
				.Build();

#pragma warning disable 4014
			_host.RunAsync(_cancellationTokenSource.Token);
#pragma warning restore 4014

			//make service client
			var binding = new BasicHttpBinding();

			var endpointXml = new EndpointAddress(new Uri($"http://localhost:{Port}/Service.asmx"));
			var channelFactoryXml = new ChannelFactory<IService>(binding, endpointXml);
			var serviceClientXml = channelFactoryXml.CreateChannel();

			var endpointDC = new EndpointAddress(new Uri($"http://localhost:{Port}/Service.svc"));
			var channelFactoryDC = new ChannelFactory<IService>(binding, endpointDC);
			var serviceClientDc = channelFactoryDC.CreateChannel();

			var endpointWcfXml = new EndpointAddress(new Uri($"http://localhost:{Port}/Service.wsdl"));
			var channelFactoryWcfXml = new ChannelFactory<IService>(binding, endpointWcfXml);
			var serviceClientWcfXml = channelFactoryWcfXml.CreateChannel();

			_sampleServiceClients[SoapSerializer.XmlSerializer] = serviceClientXml;
			_sampleServiceClients[SoapSerializer.DataContractSerializer] = serviceClientDc;
			_sampleServiceClients[SoapSerializer.XmlWcfSerializer] = serviceClientWcfXml;
		}

		public Mock<IService> ServiceMock { get; private set; }

		public static IEnumerable<object[]> SoapSerializersList()
		{
			foreach (var soapSerializer in Enum.GetValues(typeof(SoapSerializer)))
			{
				yield return new[] { soapSerializer };
			}
		}

		public IService GetSampleServiceClient(SoapSerializer soapSerializer)
		{
			WaitForServerStarted();
			return _sampleServiceClients[soapSerializer];
		}

		public void Dispose()
		{
			_cancellationTokenSource.Cancel();
		}

		private void WaitForServerStarted()
		{
			using (var client = new TcpClient())
			{
				for (var i = 0; i < 10 && !client.Connected; i++)
				{
					try
					{
						client.Connect("localhost", Port);
						break;
					}
					catch
					{
						Thread.Sleep(100);
					}
				}
			}
		}
	}
}
