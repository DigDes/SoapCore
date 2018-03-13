using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;

namespace SoapCore.Tests
{
	[TestClass]
	public class IntegrationTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://*:5050")
					.UseStartup<Startup>()
					.Build();

				host.Run();
			});
		}

		public ITestService CreateClient()
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5050/Service.svc", Environment.MachineName)));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void Ping()
		{
			var client = CreateClient();
			var result = client.Ping("hello, world");
			Assert.AreEqual("hello, world", result);
		}

		[TestMethod]
		public void EmptyArgs()
		{
			var client = CreateClient();
			var result = client.EmptyArgs();
			Assert.AreEqual("EmptyArgs", result);
		}

		[TestMethod]
		public void SingleInt()
		{
			var client = CreateClient();
			var result = client.SingleInteger(5);
			Assert.AreEqual("5", result);
		}

		[TestMethod]
		public void AsyncMethod()
		{
			var client = CreateClient();
			var result = client.AsyncMethod().Result;
			Assert.AreEqual("hello, async", result);
		}

		[TestMethod]
		public void Nullable()
		{
			var client = CreateClient();
			Assert.IsFalse(client.IsNull(5.0d));
			Assert.IsTrue(client.IsNull(null));
		}
	}
}
