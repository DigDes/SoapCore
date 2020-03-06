using System;
using System.Collections.Generic;
using System.Net.Http;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.MessageFilter
{
	[TestClass]
	public class RawMessageFilterTests
	{
		private static TestServer _host;

		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			var host = new WebHostBuilder().UseStartup<Startup>();
			RawMessageFilterTests._host = new TestServer(host);
		}

		public ITestService CreateClient(Dictionary<string, object> headers = null)
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5051/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		[TestMethod]
		public void PingNoWsSecurity()
		{
			var body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>abc</s>
    </Ping>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var client = _host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "application/soap+xml"))
			{
				content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("action", "\"http://tempuri.org/ITestService/Ping\""));
				using (var res = _host.CreateRequest("/Service.svc").And(msg => msg.Content = content).PostAsync().Result)
				{
					Assert.IsFalse(res.IsSuccessStatusCode);
					Task.Run(async () =>
					{
						var response = await res.Content.ReadAsStringAsync();
						Assert.IsTrue(response.Contains("faultcode"));
						Assert.IsTrue(response.Contains("faultstring"));
					});
				}
			}
		}

		[TestMethod]
		public void PingWithWsSecurity()
		{
			var body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Header>
    <wsse:Security xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
      <wsse:UsernameToken>
        <wsse:Username>yourusername</wsse:Username>
        <wsse:Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"">yourpassword</wsse:Password>
      </wsse:UsernameToken>
    </wsse:Security>
  </soapenv:Header>
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>abc</s>
    </Ping>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var client = _host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "application/soap+xml"))
			{
				content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("action", "\"http://tempuri.org/ITestService/Ping\""));
				using (var res = _host.CreateRequest("/Service.svc").And(msg => msg.Content = content).PostAsync().Result)
				{
					Assert.IsTrue(res.IsSuccessStatusCode);
					Task.Run(async () =>
					{
						var response = await res.Content.ReadAsStringAsync();
						Assert.IsFalse(response.Contains("faultcode"));
						Assert.IsFalse(response.Contains("faultstring"));
					});
				}
			}
		}

		[TestMethod]
		public void PingWithInvalidWsSecurity()
		{
			var body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Header>
    <wsse:Security xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
      <wsse:UsernameToken>
        <wsse:Username>INVALID_USERNAME</wsse:Username>
        <wsse:Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"">INVALID_PASSWORD</wsse:Password>
      </wsse:UsernameToken>
    </wsse:Security>
  </soapenv:Header>
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>abc</s>
    </Ping>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var client = _host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "application/soap+xml"))
			{
				content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("action", "\"http://tempuri.org/ITestService/Ping\""));
				using (var res = _host.CreateRequest("/Service.svc").And(msg => msg.Content = content).PostAsync().Result)
				{
					Assert.IsFalse(res.IsSuccessStatusCode);
					Task.Run(async () =>
					{
						var response = await res.Content.ReadAsStringAsync();
						Assert.IsTrue(response.Contains("faultcode"));
						Assert.IsTrue(response.Contains("faultstring"));
					});
				}
			}
		}
	}
}
