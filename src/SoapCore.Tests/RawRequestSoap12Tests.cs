using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using DeepEqual.Syntax;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Model;
using SoapCore.Tests.Utilities;

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

		[TestMethod]
		public async Task Soap12DetailedFault()
		{
			var body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Body>
    <ThrowDetailedFault xmlns=""http://tempuri.org/"">
      <detailMessage>Detail message</detailMessage>
    </ThrowDetailedFault>
  </soap12:Body>
</soap12:Envelope>
";
			var bodyBytes = Encoding.UTF8.GetBytes(body);
			using (var host = CreateTestHost())
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "application/soap+xml"))
			{
				using (var response = host.CreateRequest("/Service.svc").And(msg => msg.Content = content).PostAsync().Result)
				{
					Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);

					var responseBodyStream = await response.Content.ReadAsStreamAsync();
					var document = XDocument.Load(responseBodyStream);

					var faultElement = document
						.Element(XName.Get("Envelope", "http://www.w3.org/2003/05/soap-envelope"))
						.Element(XName.Get("Body", "http://www.w3.org/2003/05/soap-envelope"))
						.Element(XName.Get("Fault", "http://www.w3.org/2003/05/soap-envelope"));

					var codeElement =
						faultElement.Element(XName.Get("Code", "http://www.w3.org/2003/05/soap-envelope"));

					Assert.IsNotNull(codeElement);
					Assert.AreEqual("s:Sender", codeElement.Value);

					var reasonElementText =
						faultElement
							.Element(XName.Get("Reason", "http://www.w3.org/2003/05/soap-envelope"))
							.Elements(XName.Get("Text", "http://www.w3.org/2003/05/soap-envelope"));

					Assert.IsNotNull(reasonElementText);
					Assert.AreEqual(1, reasonElementText.Count());
					Assert.AreEqual("test", reasonElementText.First().Value);

					var detailElement =
						faultElement.Element(XName.Get("Detail", "http://www.w3.org/2003/05/soap-envelope"));

					Assert.IsNotNull(detailElement);
					var faultDetail = detailElement.DeserializeInnerElementAs<FaultDetail>();

					faultDetail.ShouldDeepEqual(new FaultDetail
					{
						ExceptionProperty = "Detail message"
					});
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
