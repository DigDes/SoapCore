using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace SoapCore.Tests.Serialization
{
	public class ServiceFixture<TService> : IDisposable
		where TService : class
	{
		private readonly IWebHost _host;
		private readonly Dictionary<SoapSerializer, TService> _sampleServiceClients = new Dictionary<SoapSerializer, TService>();

		public ServiceFixture()
		{
			var binding = new BasicHttpBinding
			{
				MaxReceivedMessageSize = int.MaxValue,
				ReaderQuotas = XmlDictionaryReaderQuotas.Max
			};

			// start service host
			_host = new WebHostBuilder()
				.ConfigureServices(services =>
				{
					// init SampleService service mock
					ServiceMock = new Mock<TService>();
					services.AddSingleton(ServiceMock.Object);
					services.AddSoapCore();
					services.AddMvc();
				})
				.Configure(appBuilder =>
				{
#if ASPNET_21
					appBuilder.UseSoapEndpoint<TService>("/Service.svc", binding, SoapSerializer.DataContractSerializer);
					appBuilder.UseSoapEndpoint<TService>("/Service.asmx", binding, SoapSerializer.XmlSerializer);
					appBuilder.UseMvc();
#endif

#if ASPNET_30
					appBuilder.UseRouting();

					appBuilder.UseEndpoints(x =>
					{
						x.UseSoapEndpoint<TService>("/Service.svc", binding, SoapSerializer.DataContractSerializer);
						x.UseSoapEndpoint<TService>("/Service.asmx", binding, SoapSerializer.XmlSerializer);
					});
#endif
				})
				.UseKestrel()
				.UseUrls($"http://127.0.0.1:0")
				.UseContentRoot(Directory.GetCurrentDirectory())
				.Build();

			var task = _host.RunAsync();

			while (true)
			{
				if (_host != null)
				{
					if (task.IsFaulted && task.Exception != null)
					{
						throw task.Exception;
					}

					if (!task.IsCompleted || !task.IsCanceled)
					{
						if (!_host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First().EndsWith(":0"))
						{
							break;
						}
					}
				}

				Thread.Sleep(2000);
			}

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			//make service client
			var endpointXml = new EndpointAddress(new Uri($"{address}/Service.asmx"));
			var channelFactoryXml = new ChannelFactory<TService>(binding, endpointXml);
			var serviceClientXml = channelFactoryXml.CreateChannel();

			var endpointDC = new EndpointAddress(new Uri($"{address}/Service.svc"));
			var channelFactoryDC = new ChannelFactory<TService>(binding, endpointDC);
			var serviceClientDc = channelFactoryDC.CreateChannel();

			_sampleServiceClients[SoapSerializer.XmlSerializer] = serviceClientXml;
			_sampleServiceClients[SoapSerializer.DataContractSerializer] = serviceClientDc;
		}

		public Mock<TService> ServiceMock { get; private set; }

		public static IEnumerable<object[]> SoapSerializersList()
		{
			foreach (var soapSerializer in Enum.GetValues(typeof(SoapSerializer)))
			{
				yield return new[] { soapSerializer };
			}
		}

		public TService GetSampleServiceClient(SoapSerializer soapSerializer)
		{
			return _sampleServiceClients[soapSerializer];
		}

		public void Dispose()
		{
			_host.StopAsync();
		}
	}
}
