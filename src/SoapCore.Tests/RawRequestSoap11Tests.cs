using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests
{
	[TestClass]
	public class RawRequestSoap11Tests
	{
		[TestMethod]
		public async Task Soap11EmptyArgs()
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Header/>
  <soapenv:Body/>
</soapenv:Envelope>
";

			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.svc").AddHeader("SOAPAction", @"""EmptyArgs""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("EmptyArgs"));
			}
		}

		[TestMethod]
		public async Task Soap11Ping()
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
