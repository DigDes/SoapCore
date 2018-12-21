using System;
using System.ServiceModel;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.ServiceOperationTuner
{
	[TestClass]
	public class ServiceOperationTunerTests
	{
		private static CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://localhost:5054")
					.UseStartup<Startup>()
					.Build();

			host.RunAsync(_cancelTokenSource.Token);
		}

		[ClassCleanup]
		public static void StopServer()
		{
			_cancelTokenSource.Cancel();
		}

		[TestInitialize]
		public void Reset()
		{
			TestServiceOperationTuner.Reset();
		}

		public ITestService CreateClient(string pingValue)
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5054/Service.svc", "localhost")));
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
