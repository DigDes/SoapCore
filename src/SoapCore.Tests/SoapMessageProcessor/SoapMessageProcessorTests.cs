using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.SoapMessageProcessor
{
	[TestClass]
	public class SoapMessageProcessorTests
	{
		[TestMethod]
		public async Task ReplaceResponseWithCustomEmptyMessageAsync()
		{
			var pingValue = "abc";
			var body = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>{pingValue}</s>
    </Ping>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/ServiceWithProcessor.svc").AddHeader("SOAPAction", @"""Ping""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("<s:Body />"));
			}
		}

		[TestMethod]
		public async Task ReplaceResponseWithPongMessageAsync()
		{
			var pingValue = "abc";
			var body = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>{pingValue}</s>
    </Ping>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/ServiceWithPongProcessor.svc").AddHeader("SOAPAction", @"""Ping""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Trace.TraceInformation(response);
				Assert.IsTrue(response.Contains("<PongResult>"));
			}
		}

		[TestMethod]
		public async Task AssertThatTheOrdinaryHandlingAlsoWorksAsync()
		{
			var pingValue = "abc";
			var body = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>{pingValue}</s>
    </Ping>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.svc").AddHeader("SOAPAction", @"""Ping""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains(pingValue));
			}
		}

		private TestServer CreateTestHost()
		{
			var webHostBuilder = new WebHostBuilder()
				.UseStartup<Startup>();
			return new TestServer(webHostBuilder);
		}
	}
}
