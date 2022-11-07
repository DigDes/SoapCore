using System.Net.Http;
using System.Net.Http.Headers;
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

		[TestMethod]
		public void Soap11PingWithMixedNamespacing()
		{
			var pingValue = "Lorem ipsum";
			var body = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>
<SOAP-ENV:Envelope xmlns:SOAPSDK1=""http://www.w3.org/2001/XMLSchema""
                   xmlns:SOAPSDK2=""http://www.w3.org/2001/XMLSchema-instance""
                   xmlns:SOAPSDK3=""http://schemas.xmlsoap.org/soap/encoding/""
                   xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"">
	<SOAP-ENV:Body SOAP-ENV:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
		<SOAPSDK4:Ping xmlns:SOAPSDK4=""http://tempuri.org/"">
			<s>{pingValue}</s>
		</SOAPSDK4:Ping>
	</SOAP-ENV:Body>
</SOAP-ENV:Envelope>
";
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.svc").AddHeader("SOAPAction", @"""Ping""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = res.Content.ReadAsStringAsync().Result;
				Assert.IsTrue(response.Contains(pingValue));
			}
		}

		[TestMethod]
		public async Task Soap11PingInMultipart()
		{
			var pingValue = "abc";
			var soapBody = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
				<soapenv:Body>
					<Ping xmlns=""http://tempuri.org/"">
						<s>{pingValue}</s>
					</Ping>
				</soapenv:Body>
			</soapenv:Envelope>";

			using var host = CreateTestHost();
			using var client = host.CreateClient();
			using var multipartContent = new MultipartContent("related");
			multipartContent.Headers.ContentType!.Parameters.Add(new NameValueHeaderValue("type", "\"text/xml\""));

			using var soapContent = new StringContent(soapBody, Encoding.UTF8, "text/xml");
			soapContent.Headers.Add("SOAPAction", @"""Ping""");

			using var extraContent = new StringContent("some text payload", Encoding.UTF8, "text/plain");

			multipartContent.Add(soapContent);
			multipartContent.Add(extraContent);

			using var res = await host.CreateRequest("/Service.svc").And(msg => msg.Content = multipartContent).PostAsync();

			res.EnsureSuccessStatusCode();
			var response = await res.Content.ReadAsStringAsync();
			Assert.IsTrue(response.Contains(pingValue));
		}

		[TestMethod]
		public async Task Soap11PingResponseBodyEncodingSameAsWriteEncoding()
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
			using (var content = new StringContent(body, Encoding.GetEncoding("ISO-8859-1"), "text/xml"))
			{
				var requestBuilder = host.CreateRequest("/ServiceWithDifferentEncodings.asmx").AddHeader("SOAPAction", @"""Ping""").And(msg => msg.Content = content);
				using (var res = requestBuilder.PostAsync().Result)
				{
					res.EnsureSuccessStatusCode();

					var response = await res.Content.ReadAsStringAsync();

					Assert.IsTrue(response.Contains(pingValue));
					Assert.IsTrue(response.Contains("<?xml version=\"1.0\" encoding=\"utf-8\"?>"));
				}
			}
		}

		[TestMethod]
		public async Task Soap11PingWithOverwrittenContentType()
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
			using (var content = new StringContent(body, Encoding.GetEncoding("ISO-8859-1"), "text/xml"))
			{
				var requestBuilder = host.CreateRequest("/ServiceWithOverwrittenContentType.asmx").AddHeader("SOAPAction", @"""Ping""").And(msg => msg.Content = content);
				using (var res = requestBuilder.PostAsync().Result)
				{
					res.EnsureSuccessStatusCode();

					var response = await res.Content.ReadAsStringAsync();
					var requestContentType = res.RequestMessage.Content.Headers.ContentType.ToString();
					var responseContentType = res.Content.Headers.ContentType.ToString();

					Assert.IsTrue(response.Contains(pingValue));
					Assert.AreNotEqual(requestContentType, responseContentType);
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
