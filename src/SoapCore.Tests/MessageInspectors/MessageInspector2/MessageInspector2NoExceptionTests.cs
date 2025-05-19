using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.MessageInspectors.MessageInspector2
{
	[TestClass]
	public class MessageInspector2NoExceptionTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://localhost:7052")
					.UseStartup<Startup>()
					.UseSetting("InspectorStyle", InspectorStyle.MessageInspector2NoException.ToString())
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
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:7052/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void AfterReceivedRequestCalled()
		{
			Assert.IsFalse(MessageInspector2Mock.AfterReceivedRequestCalled);
			var client = CreateClient();
			var result = client.Ping("Hello World");
			Assert.IsTrue(MessageInspector2Mock.AfterReceivedRequestCalled);
		}

		[TestMethod]
		public void BeforeSendReplyCalled()
		{
			Assert.IsFalse(MessageInspector2Mock.BeforeSendReplyCalled);
			var client = CreateClient();
			var result = client.Ping("Hello World");
			Assert.IsTrue(MessageInspector2Mock.BeforeSendReplyCalled);
		}

		[TestMethod]
		public void BeforeSendReplyCalledEvenIfServiceThrowsException()
		{
			Assert.IsFalse(MessageInspector2Mock.BeforeSendReplyCalled);
			var client = CreateClient();
			Assert.ThrowsException<FaultException>(client.ThrowException);
			Assert.IsTrue(MessageInspector2Mock.BeforeSendReplyCalled);
		}
	}
}
