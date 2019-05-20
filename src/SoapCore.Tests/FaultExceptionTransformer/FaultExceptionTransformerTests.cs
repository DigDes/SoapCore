using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.FaultExceptionTransformer
{
	[TestClass]
	public class FaultExceptionTransformerTests
	{
		private static IWebHost _host;

		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				_host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://127.0.0.1:0")
					.UseStartup<Startup>()
					.Build();

				_host.Run();
			});

			while (_host == null || _host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First().EndsWith(":0"))
			{
				Thread.Sleep(2000);
			}
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
		public void CustomFaultMessage()
		{
			try
			{
				var client = CreateClient();
				client.ThrowExceptionWithMessage("foo");
			}
			catch (FaultException exception)
			{
				Assert.AreEqual("foo", exception.Message);

				var messageFault = exception.CreateMessageFault();
				var detail = messageFault.GetDetail<TestFault>();

				Assert.IsNotNull(detail);

				Assert.AreEqual("foo:bar", detail.AdditionalProperty);
			}
		}
	}
}
