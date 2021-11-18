using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.MessageInspectors.MessageInspector3
{
	[TestClass]
	public class MessageInspector3Tests
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
					.UseSetting("InspectorStyle", InspectorStyle.MessageInspector3.ToString())
					.Build();

				host.Run();
			}).Wait(1000);
		}

		[TestInitialize]
		public void Reset()
		{
			MessageInspector3Mock.Reset();
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
			Assert.IsFalse(MessageInspector3Mock.AfterReceivedRequestCalled);
			var client = CreateClient();
			var result = client.Ping("Fail");
			Assert.AreEqual("Failed", result);
			Assert.IsTrue(MessageInspector3Mock.AfterReceivedRequestCalled);
		}

		[TestMethod]
		public void BeforeSendReplyShouldNotBeCalled()
		{
			Assert.IsFalse(MessageInspector3Mock.BeforeSendReplyCalled);
			var client = CreateClient();
			var result = client.Ping("Hello World");
			Assert.IsFalse(MessageInspector3Mock.BeforeSendReplyCalled);
		}
	}
}
