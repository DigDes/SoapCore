using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.MessageInspectors.MessageInspector2
{
	[TestClass]
	public class MessageInspector2Tests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel(x => x.AllowSynchronousIO = true)
					.UseUrls("http://localhost:7051")
					.UseStartup<Startup>()
					.UseSetting("InspectorStyle", InspectorStyle.MessageInspector2.ToString())
					.Build();

				host.Run();
			}).Wait(1000);
		}

		[TestInitialize]
		public void Reset()
		{
			MessageInspector2Mock.Reset();
		}

		public ITestService CreateClient()
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:7051/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		[ExpectedException(typeof(FaultException))]
		public void AfterReceivedRequestCalled()
		{
			Assert.IsFalse(MessageInspector2Mock.AfterReceivedRequestCalled);
			var client = CreateClient();
			var result = client.Ping("Hello World");
			Assert.IsTrue(MessageInspector2Mock.AfterReceivedRequestCalled);
		}

		[TestMethod]
		[ExpectedException(typeof(FaultException))]
		public void BeforeSendReplyShouldNotBeCalled()
		{
			Assert.IsFalse(MessageInspector2Mock.BeforeSendReplyCalled);
			var client = CreateClient();
			var result = client.Ping("Hello World");
			Assert.IsFalse(MessageInspector2Mock.BeforeSendReplyCalled);
		}

		[TestMethod]
		public void AfterReceivedThrowsException()
		{
			var client = CreateClient();
			Assert.ThrowsException<FaultException>(() => client.Ping("Hello World"));
		}
	}
}
