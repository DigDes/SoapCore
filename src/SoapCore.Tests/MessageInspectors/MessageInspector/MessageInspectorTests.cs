using System;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.MessageInspectors.MessageInspector
{
	[Obsolete]
	[TestClass]
	public class MessageInspectorTests
	{
		private static IWebHost _host;

		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			_host = new WebHostBuilder()
				.UseKestrel()
				.UseUrls("http://127.0.0.1:0")
				.UseStartup<Startup>()
				.UseSetting("InspectorStyle", InspectorStyle.MessageInspector.ToString())
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
			MessageInspectorMock.Reset();
		}

		public ITestService CreateClient()
		{
			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("{0}/Service.svc", address)));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		[ExpectedException(typeof(FaultException))]
		public void AfterReceivedRequestCalled()
		{
			Assert.IsFalse(MessageInspectorMock.AfterReceivedRequestCalled);
			var client = CreateClient();
			var result = client.Ping("Hello World");
			Assert.IsTrue(MessageInspectorMock.AfterReceivedRequestCalled);
		}

		[TestMethod]
		[ExpectedException(typeof(FaultException))]
		public void BeforeSendReplyShouldNotBeCalled()
		{
			Assert.IsFalse(MessageInspectorMock.BeforeSendReplyCalled);
			var client = CreateClient();
			var result = client.Ping("Hello World");
			Assert.IsFalse(MessageInspectorMock.BeforeSendReplyCalled);
		}

		[TestMethod]
		public void AfterReceivedThrowsException()
		{
			var client = CreateClient();
			Assert.ThrowsException<FaultException>(() => client.Ping("Hello World"));
		}
	}
}
