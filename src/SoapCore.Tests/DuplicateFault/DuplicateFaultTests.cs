using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.DuplicateFault
{
	// https://github.com/DigDes/SoapCore/issues/1192
	[TestClass]
	public class DuplicateFaultTests
	{
		private const string FaultMessage = "Test fault message";
		private const string ErrorLogMessage = "An error occurred processing the message";

		[TestMethod]
		public async Task ProvideFaultIsCalledOnce()
		{
			using (var host = CreateTestHost())
			{
				await PostFaultingRequestAsync(host);

				var faultExceptionTransformer = host.Services.GetRequiredService<CountingFaultExceptionTransformer>();

				Assert.AreEqual(1, faultExceptionTransformer.ProvideFaultCallCount);
			}
		}

		[TestMethod]
		public async Task ErrorIsLoggedOnce()
		{
			using (var host = CreateTestHost())
			{
				await PostFaultingRequestAsync(host);

				var loggerProvider = host.Services.GetRequiredService<CountingLoggerProvider>();

				Assert.AreEqual(1, loggerProvider.Errors.Count(error => error == ErrorLogMessage));
			}
		}

		[TestMethod]
		public async Task BeforeSendReplyIsCalledOnce()
		{
			using (var host = CreateTestHost())
			{
				await PostFaultingRequestAsync(host);

				var messageInspector = host.Services.GetRequiredService<StampingMessageInspector>();

				Assert.AreEqual(1, messageInspector.AfterReceiveRequestCallCount);
				Assert.AreEqual(1, messageInspector.BeforeSendReplyCallCount);
			}
		}

		[TestMethod]
		public async Task BeforeSendReplyIsCalledOnceWhenTheResponseFilterThrows()
		{
			using (var host = CreateTestHost(registerThrowingResponseFilter: true))
			{
				await PostPingRequestAsync(host);

				var messageInspector = host.Services.GetRequiredService<StampingMessageInspector>();

				Assert.AreEqual(1, messageInspector.AfterReceiveRequestCallCount);
				Assert.AreEqual(1, messageInspector.BeforeSendReplyCallCount);
			}
		}

		[TestMethod]
		public async Task MessageHandedToBeforeSendReplyIsTheMessageThatIsSent()
		{
			using (var host = CreateTestHost())
			{
				var response = await PostFaultingRequestAsync(host);

				Assert.IsTrue(response.Contains(FaultMessage), "The fault should carry the reason of the exception");
				Assert.IsTrue(response.Contains(StampingMessageInspector.HeaderName), "The fault should carry the header added by BeforeSendReply");
				Assert.IsTrue(response.Contains(StampingMessageInspector.HeaderValue), "The fault should carry the value written by BeforeSendReply");
			}
		}

		private TestServer CreateTestHost(bool registerThrowingResponseFilter = false)
		{
			var webHostBuilder = new WebHostBuilder()
				.UseStartup<Startup>()
				.UseSetting(Startup.ThrowingResponseFilterSetting, registerThrowingResponseFilter.ToString());

			return new TestServer(webHostBuilder);
		}

		private Task<string> PostFaultingRequestAsync(TestServer host)
		{
			var bodyContent = $@"<ThrowExceptionWithMessage xmlns=""http://tempuri.org/"">
      <message>{FaultMessage}</message>
    </ThrowExceptionWithMessage>";

			return PostAsync(host, "ThrowExceptionWithMessage", bodyContent);
		}

		private Task<string> PostPingRequestAsync(TestServer host)
		{
			var bodyContent = @"<Ping xmlns=""http://tempuri.org/"">
      <s>Hello World</s>
    </Ping>";

			return PostAsync(host, "Ping", bodyContent);
		}

		private async Task<string> PostAsync(TestServer host, string soapAction, string bodyContent)
		{
			var body = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    {bodyContent}
  </soapenv:Body>
</soapenv:Envelope>
";

			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = await host.CreateRequest("/Service.svc").AddHeader("SOAPAction", $@"""{soapAction}""").And(msg => msg.Content = content).PostAsync())
			{
				Assert.AreEqual(HttpStatusCode.InternalServerError, res.StatusCode);

				return await res.Content.ReadAsStringAsync();
			}
		}
	}
}
