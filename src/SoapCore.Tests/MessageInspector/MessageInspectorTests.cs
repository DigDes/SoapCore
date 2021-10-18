using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.MessageInspector
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

		public ITestService CreateClient(Dictionary<string, object> headers = null)
		{
			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("{0}/Service.svc", address)));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			channelFactory.Endpoint.EndpointBehaviors.Add(new CustomHeadersEndpointBehavior(headers));
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void AfterReceivedRequestCalled()
		{
			Assert.IsFalse(MessageInspectorMock.AfterReceivedRequestCalled);
			var client = CreateClient(new Dictionary<string, object>() { { "header1-key", "header1-value" } });
			var result = client.Ping("hello, world");
			Assert.IsTrue(MessageInspectorMock.AfterReceivedRequestCalled);
		}

		[TestMethod]
		public void AfterReceivedRequestHasAction()
		{
			Assert.IsNull(MessageInspectorMock.Action);
			var client = CreateClient(new Dictionary<string, object>() { { "header1-key", "header1-value" } });
			var result = client.Ping("hello, world");
			Assert.IsNotNull(MessageInspectorMock.Action);
		}

		[TestMethod]
		public void BeforeSendReplyCalled()
		{
			Assert.IsFalse(MessageInspectorMock.BeforeSendReplyCalled);
			var client = CreateClient(new Dictionary<string, object>() { { "header1-key", "header1-value" } });
			var result = client.Ping("hello, world");
			Assert.IsTrue(MessageInspectorMock.BeforeSendReplyCalled);
		}

		[TestMethod]
		public void SingleSoapHeader()
		{
			var client = CreateClient(new Dictionary<string, object>() { { "header1-key", "header1-value" } });
			var result = client.Ping("hello, world");
			var msg = MessageInspectorMock.LastReceivedMessage;
			var index = msg.Headers.FindHeader("header1-key", "SoapCore");
			Assert.AreEqual(msg.Headers.GetHeader<string>(index), "header1-value");
		}

		[TestMethod]
		public void MultipleSoapHeaders()
		{
			var client = CreateClient(new Dictionary<string, object>() { { "header1-key", "header1-value" }, { "header2-key", 2 } });
			var result = client.Ping("hello, world");
			var msg = MessageInspectorMock.LastReceivedMessage;
			Assert.AreEqual(msg.Headers.GetHeader<string>(msg.Headers.FindHeader("header1-key", "SoapCore")), "header1-value");
			Assert.AreEqual(msg.Headers.GetHeader<int>(msg.Headers.FindHeader("header2-key", "SoapCore")), 2);
		}

		[TestMethod]
		public void ComplexSoapHeader()
		{
			var client = CreateClient(new Dictionary<string, object>()
			{
				{
					"complex", new ComplexModelInput()
					{
						StringProperty = "hello, world",
						IntProperty = 1000,
						ListProperty = new List<string> { "test", "list", "of", "strings" },
						DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
					}
				}
			});

			var result = client.Ping(string.Empty);
			var msg = MessageInspectorMock.LastReceivedMessage;
			var complex = msg.Headers.GetHeader<ComplexModelInput>(msg.Headers.FindHeader("complex", "SoapCore"));
			Assert.AreEqual(complex.StringProperty, "hello, world");
			Assert.AreEqual(complex.IntProperty, 1000);
			CollectionAssert.AreEqual(complex.ListProperty, new List<string> { "test", "list", "of", "strings" });
		}
	}
}
