using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.ActionFilter
{
	[TestClass]
	public class ActionFilterTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel(x => x.AllowSynchronousIO = true)
					.UseUrls("http://localhost:5052")
					.UseStartup<Startup>()
					.Build();
				host.Run();
			}).Wait(1000);
		}

		public ITestService CreateClient(Dictionary<string, object> headers = null)
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5052/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void ModelWasAlteredInActionFilter()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 123,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient();
			var result = client.ComplexParamWithActionFilter(inputModel);
			Assert.AreNotEqual(inputModel.StringProperty, result.StringProperty);
			Assert.AreNotEqual(inputModel.IntProperty, result.IntProperty);
		}
	}
}
