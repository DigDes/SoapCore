using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Model;

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
					.UseKestrel(x => x.AllowSynchronousIO = true)
					.UseUrls("http://localhost:5050")
					.UseStartup<Startup>()
					.Build();

				host.Run();
			}).Wait(1000);
		}

		[TestMethod]
		public void PingWithCaseInsensitivePath()
		{
			var client = CreateClient(caseInsensitivePath: true);
			var result = client.Ping("hello, world");
			Assert.AreEqual("hello, world", result);
		}

		[TestMethod]
		public void PingSoap12()
		{
			var client = CreateSoap12Client();
			var result = client.Ping("hello, world");
			Assert.AreEqual("hello, world", result);
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

		[TestMethod]
		public void OverloadedMethod()
		{
			var client = CreateClient();
			Assert.AreEqual("Overload(double)", client.Overload(5.0d));
			Assert.AreEqual("Overload(string)", client.Overload("hello, world"));
		}

		[TestMethod]
		public void OperationNameOverride()
		{
			var client = CreateClient();
			Assert.IsTrue(client.OperationName());
		}

		[TestMethod]
		public void OutParam()
		{
			var client = CreateClient();
			string message;
			client.OutParam(out message);
			Assert.AreEqual("hello, world", message);
		}

		[TestMethod]
		public void OutComplexParam()
		{
			var client = CreateClient();
			client.OutComplexParam(out ComplexModelInput test);
			Assert.AreEqual(test.IntProperty, 10);
		}

		[TestMethod]
		public void RefParam()
		{
			var client = CreateClient();
			string message = string.Empty;
			client.RefParam(ref message);
			Assert.AreEqual("hello, world", message);
		}

		[TestMethod]
		public void ThrowsFaultException()
		{
			var client = CreateClient();
			Assert.ThrowsException<FaultException>(() =>
			{
				client.ThrowException();
			});
		}

		[TestMethod]
		public void ExceptionMessage()
		{
			var client = CreateClient();
			var e = Assert.ThrowsException<FaultException>(() =>
			{
				client.ThrowExceptionWithMessage("Your error message here");
			});
			Assert.AreEqual("Your error message here", e.Message);
		}

		[TestMethod]
		public void ExceptionMessageSoap12()
		{
			var client = CreateSoap12Client();

			var e = Assert.ThrowsException<FaultException>(() =>
			{
				client.ThrowExceptionWithMessage("Your error message here");
			});

			Assert.AreEqual("Your error message here", e.Message);
		}

		[TestMethod]
		public void ThrowsDetailedFault()
		{
			var client = CreateClient();
			var e = Assert.ThrowsException<FaultException<FaultDetail>>(() =>
			{
				client.ThrowDetailedFault("Detail message");
			});
			Assert.IsNotNull(e.Detail);
			Assert.AreEqual("Detail message", e.Detail.ExceptionProperty);
		}

		[TestMethod]
		public void ThrowsDetailedSoap12Fault()
		{
			var client = CreateSoap12Client();

			var e = Assert.ThrowsException<FaultException<FaultDetail>>(() =>
			{
				client.ThrowDetailedFault("Detail message");
			});

			Assert.IsNotNull(e.Detail);
			Assert.AreEqual("Detail message", e.Detail.ExceptionProperty);
		}

		private ITestService CreateClient(bool caseInsensitivePath = false)
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(
				string.Format("http://{0}:5050/{1}.svc", "localhost", caseInsensitivePath ? "serviceci" : "Service")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		private ITestService CreateSoap12Client()
		{
			var transport = new HttpTransportBindingElement();
			var textencoding = new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, System.Text.Encoding.UTF8);
			var binding = new CustomBinding(textencoding, transport);
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5050/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}
	}
}
