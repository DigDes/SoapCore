using System;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests
{
	[TestClass]
	public class RawRequestSoap12Tests
	{
		[TestMethod]
		public void Soap12PingWithActionInHeader()
		{
			var body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>abc</s>
    </Ping>
  </soap12:Body>
</soap12:Envelope>
";
			var bodyBytes = Encoding.UTF8.GetBytes(body);
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "application/soap+xml"))
			{
				content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("action", "\"http://tempuri.org/ITestService/Ping\""));
				using (var res = host.CreateRequest("/Service.svc").And(msg => msg.Content = content).PostAsync().Result)
				{
					res.EnsureSuccessStatusCode();
				}
			}
		}

		[TestMethod]
		public void Soap12PingNoActionInHeader()
		{
			var body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>abc</s>
    </Ping>
  </soap12:Body>
</soap12:Envelope>
";
			var bodyBytes = Encoding.UTF8.GetBytes(body);
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "application/soap+xml"))
			{
				using (var res = host.CreateRequest("/Service.svc").And(msg => msg.Content = content).PostAsync().Result)
				{
					res.EnsureSuccessStatusCode();
				}
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
