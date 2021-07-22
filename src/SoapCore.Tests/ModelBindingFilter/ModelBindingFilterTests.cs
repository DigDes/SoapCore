using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.ModelBindingFilter
{
	[TestClass]
	public class ModelBindingFilterTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://localhost:5053")
					.UseStartup<Startup>()
					.Build();
				host.Run();
			}).Wait(1000);
		}

		public ITestService CreateClient(Dictionary<string, object> headers = null)
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5053/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void ModelWasAlteredInModelBindingFilter()
		{
			var inputModel = new ComplexModelInputForModelBindingFilter
			{
				StringProperty = "string property test value",
				IntProperty = 123,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient();
			var result = client.ComplexParamWithModelBindingFilter(inputModel);
			Assert.AreNotEqual(inputModel.StringProperty, result.StringProperty);
			Assert.AreNotEqual(inputModel.IntProperty, result.IntProperty);
		}

		[TestMethod]
		public void ModelWasNotAlteredInModelBindingFilter()
		{
			var inputModel = new ComplexModelInput
			{
				StringProperty = "string property test value",
				IntProperty = 123,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var client = CreateClient();
			var result = client.ComplexParam(inputModel);
			Assert.AreEqual(inputModel.StringProperty, result.StringProperty);
			Assert.AreEqual(inputModel.IntProperty, result.IntProperty);
		}
	}
}
