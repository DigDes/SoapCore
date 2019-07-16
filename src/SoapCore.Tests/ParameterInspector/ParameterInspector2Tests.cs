using System;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.ParameterInspector
{
	[TestClass]
	public class ParameterInspector2Tests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://localhost:8921")
					.UseStartup<Startup>()
					.Build();

				host.Run();
			}).Wait(1000);
		}

		public ITestService CreateClient()
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:8921/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void InputArgumentTest()
		{
			var client = CreateClient();

			Assert.AreEqual("BeforeCall", client.Ping(Guid.NewGuid().ToString()));
		}

		[TestMethod]
		public void OutputArgumentTest()
		{
			var client = CreateClient();

			client.OutParam(out var message);

			Assert.AreEqual("AfterCall", message);
		}
	}
}
