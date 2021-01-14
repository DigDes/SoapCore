using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.MessageContract
{
	[TestClass]
	public class RawRequestSoap11Tests
	{
		[TestMethod]
		public async Task Soap11MessageContractEmpty()
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Header/>
  <soapenv:Body/>
</soapenv:Envelope>
";

			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.svc").AddHeader("SOAPAction", @"""EmptyRequest""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("EmptyRequest"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContract()
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org"">
  <soapenv:Header/>
  <soapenv:Body>	  
    <tem:MessageContractRequest>	   
        <tem:ReferenceNumber>1</tem:ReferenceNumber>
    </tem:MessageContractRequest>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.asmx").AddHeader("SOAPAction", @"""DoRequest""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("MessageContractResponse"));
				Assert.IsTrue(response.Contains("ReferenceNumber>1</"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractNotWrapped()
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org"">
  <soapenv:Header/>
  <soapenv:Body>	  
    <tem:ReferenceNumber>1</tem:ReferenceNumber>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.asmx").AddHeader("SOAPAction", @"""DoRequest2""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("ReferenceNumber"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractGetWSDL()
		{
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var res = host.CreateRequest("/Service.asmx?wsdl").GetAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("wsdl"));
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
