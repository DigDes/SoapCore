using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.XmlNodeInputOutput
{
	/*
	 * This test refers to issue https://github.com/DigDes/SoapCore/issues/908
	 * User has a service with an XmlNode parameter and the value sent to the function is always null.
	 */

	[TestClass]
	public class XmlNodeInputOutputTests
	{
		[TestMethod]
		public async Task SendXmlInputGetXmlOutputAsync()
		{
			string xmlInput = "<Request><Input>Hello</Input><Type>Test</Type></Request>",
				strLogin = "Login",
				strPassword = "Password";
			var body = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <ProcessRequest xmlns=""http://tempuri.org/"">
      <login>{strLogin}</login>
      <password>{strPassword}</password>
      <requestXml>{xmlInput}</requestXml>
   </ProcessRequest>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.svc").AddHeader("SOAPAction", @"""ProcessRequest""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();

				//XML comes back as formatted, need to clear any newlines and replace any double spaces
				Assert.IsTrue(response.Replace(System.Environment.NewLine, string.Empty).Replace("  ", string.Empty).Contains(xmlInput));
			}
		}

		[TestMethod]
		public async Task GetXmlOutputAsync()
		{
			var body = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <GetRequest xmlns=""http://tempuri.org/"">      
   </GetRequest>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.svc").AddHeader("SOAPAction", @"""GetRequest""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("A response"));
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
