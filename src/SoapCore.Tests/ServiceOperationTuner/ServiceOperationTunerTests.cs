using System;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.ServiceOperationTuner
{
	[TestClass]
	public class ServiceOperationTunerTests
	{
		private static IWebHost _host;

		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			_host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://127.0.0.1:0")
					.UseStartup<Startup>()
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
		}

		[ClassCleanup]
		public static async Task StopServer()
		{
			await _host.StopAsync();
		}

		[TestInitialize]
		public void Reset()
		{
			TestServiceOperationTuner.Reset();
		}

		public ITestService CreateClient(string pingValue)
		{
			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("{0}/Service.svc", address)));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			channelFactory.Endpoint.EndpointBehaviors.Add(new CustomHeadersEndpointBehavior(pingValue));
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void PassParameterViaHttpHeader()
		{
			Assert.IsFalse(TestServiceOperationTuner.IsCalled);
			Assert.IsFalse(TestServiceOperationTuner.IsSetPingValue);

			string expected = "ping value";
			var client = CreateClient(expected);
			var result = client.PingWithServiceOperationTuning();

			Assert.IsTrue(TestServiceOperationTuner.IsCalled);
			Assert.IsTrue(TestServiceOperationTuner.IsSetPingValue);
			Assert.AreEqual(expected, result);
		}

		[TestMethod]
		public void CheckThatPingIsNotAffectedByOperationTuner()
		{
			Assert.IsFalse(TestServiceOperationTuner.IsCalled);
			Assert.IsFalse(TestServiceOperationTuner.IsSetPingValue);

			string expected = "ping";
			var client = CreateClient("bla-bla-bla");
			var result = client.Ping("ping");

			Assert.IsTrue(TestServiceOperationTuner.IsCalled);
			Assert.IsFalse(TestServiceOperationTuner.IsSetPingValue);
			Assert.AreEqual(expected, result);
		}
	}
}
