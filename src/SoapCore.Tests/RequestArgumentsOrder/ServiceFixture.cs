using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace SoapCore.Tests.RequestArgumentsOrder
{
	public sealed class ServiceFixture<TOriginalParametersOrderService, TReversedParametersOrderService> : IDisposable
		where TOriginalParametersOrderService : class
		where TReversedParametersOrderService : class
	{
		private readonly IWebHost _host;
		private readonly Dictionary<SoapSerializer, TOriginalParametersOrderService> _originalRequestArgumentsOrderClients;
		private readonly Dictionary<SoapSerializer, TReversedParametersOrderService> _reversedRequestArgumentsOrderClients;

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
					// init service mock
					ServiceMock = new Mock<TOriginalParametersOrderService>();
					services.AddSingleton(ServiceMock.Object);
					services.AddSoapCore();
					services.AddMvc();
				})
				.Configure(appBuilder =>
				{
#if !NETCOREAPP3_0_OR_GREATER
					appBuilder.UseSoapEndpoint<TOriginalParametersOrderService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
					appBuilder.UseSoapEndpoint<TOriginalParametersOrderService>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
					appBuilder.UseMvc();
#else
					appBuilder.UseRouting();

					appBuilder.UseEndpoints(x =>
					{
						x.UseSoapEndpoint<TOriginalParametersOrderService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
						x.UseSoapEndpoint<TOriginalParametersOrderService>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
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

			//make clients
			_originalRequestArgumentsOrderClients = InitClients<TOriginalParametersOrderService>(binding, address);
			_reversedRequestArgumentsOrderClients = InitClients<TReversedParametersOrderService>(binding, address);
		}

		public Mock<TOriginalParametersOrderService> ServiceMock { get; private set; }

		public static IEnumerable<object[]> SoapSerializersList()
		{
			foreach (var soapSerializer in Enum.GetValues(typeof(SoapSerializer)))
			{
				yield return new[] { soapSerializer };
			}
		}

		public TOriginalParametersOrderService GetOriginalRequestArgumentsOrderClient(SoapSerializer soapSerializer)
		{
			return _originalRequestArgumentsOrderClients[soapSerializer];
		}

		public TReversedParametersOrderService GetReversedRequestArgumentsOrderClient(SoapSerializer soapSerializer)
		{
			return _reversedRequestArgumentsOrderClients[soapSerializer];
		}

		public void Dispose()
		{
			_host.StopAsync();
			_host.Dispose();
		}

		private Dictionary<SoapSerializer, TService> InitClients<TService>(BasicHttpBinding binding, string address)
		{
			var endpointXml = new EndpointAddress(new Uri($"{address}/Service.asmx"));
			var channelFactoryXml = new ChannelFactory<TService>(binding, endpointXml);
			var serviceClientXml = channelFactoryXml.CreateChannel();

			var endpointDC = new EndpointAddress(new Uri($"{address}/Service.svc"));
			var channelFactoryDC = new ChannelFactory<TService>(binding, endpointDC);
			var serviceClientDc = channelFactoryDC.CreateChannel();

			return new Dictionary<SoapSerializer, TService>
			{
				[SoapSerializer.XmlSerializer] = serviceClientXml,
				[SoapSerializer.DataContractSerializer] = serviceClientDc
			};
		}
	}
}
