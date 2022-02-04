using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.MessageContract.Models;

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

			using (var host = CreateTestHost(typeof(TestService)))
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
			using (var host = CreateTestHost(typeof(TestService)))
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
			using (var host = CreateTestHost(typeof(TestServiceNotWrapped)))
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.asmx").AddHeader("SOAPAction", @"""PullData""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("ReferenceNumber"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractArrayOfIntParam()
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org"">
  <soapenv:Header/>
  <soapenv:Body>
	<tem:ArrayOfIntMethod>
    <tem:arrayOfIntParam>1,2</tem:arrayOfIntParam>
	</tem:ArrayOfIntMethod>
  </soapenv:Body>
</soapenv:Envelope>
";

			using (var host = CreateTestHost(typeof(ArrayOfIntService)))
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = await host.CreateRequest("/Service.asmx").AddHeader("SOAPAction", @"""ArrayOfIntMethod""").And(msg => msg.Content = content).PostAsync())
			{
				res.EnsureSuccessStatusCode();
				var resultMessage = await res.Content.ReadAsStringAsync();

				//the result should be an empty array
				Assert.IsTrue(resultMessage.Contains("<ArrayOfIntMethodResult />"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractArrayOfIntParamWrapped()
		{
			const string body2 = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org"">
  <soapenv:Header/>
  <soapenv:Body>
	<tem:ArrayOfIntMethod>
    <tem:arrayOfIntParam><tem:int>1</tem:int><tem:int>2</tem:int></tem:arrayOfIntParam>
	</tem:ArrayOfIntMethod>
  </soapenv:Body>
</soapenv:Envelope>
";

			using (var host = CreateTestHost(typeof(ArrayOfIntService)))
			using (var client = host.CreateClient())
			using (var content = new StringContent(body2, Encoding.UTF8, "text/xml"))
			using (var res = await host.CreateRequest("/Service.asmx").AddHeader("SOAPAction", @"""ArrayOfIntMethod""").And(msg => msg.Content = content).PostAsync())
			{
				res.EnsureSuccessStatusCode();
				var resultMessage = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(resultMessage.Contains("<int>1</int>"));
				Assert.IsTrue(resultMessage.Contains("<int>2</int>"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractComplexNotWrapped()
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org"">
  <soapenv:Header/>
  <soapenv:Body>	  
    <tem:PostDataBodyMember>
		<tem:StringProperty>Test</tem:StringProperty>
		<tem:IntProperty>42</tem:IntProperty>
		<tem:ListProperty />
		<tem:DateTimeOffsetProperty>2021-11-10T13:35:17.6062448+03:00</tem:DateTimeOffsetProperty>
    </tem:PostDataBodyMember>
  </soapenv:Body>
</soapenv:Envelope>
";
			using (var host = CreateTestHost(typeof(TestServiceComplexNotWrapped)))
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/Service.asmx").AddHeader("SOAPAction", @"""PostData""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();
				Assert.IsTrue(response.Contains("<ReferenceNumber xmlns=\"http://tempuri.org\">42</ReferenceNumber>"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractGetWSDL()
		{
			using (var host = CreateTestHost(typeof(TestService)))
			using (var client = host.CreateClient())
			using (var res = host.CreateRequest("/Service.asmx?wsdl").GetAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();

				Assert.IsTrue(response.Contains("wsdl"));
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractCheckWSDLElementsNotWrapped()
		{
			using (var host = CreateTestHost(typeof(TestServiceNotWrapped)))
			using (var client = host.CreateClient())
			using (var res = host.CreateRequest("/Service.asmx?wsdl").GetAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();

				var root = new XmlDocument();
				root.LoadXml(response);

				var nsmgr = new XmlNamespaceManager(root.NameTable);
				nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
				nsmgr.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");

				//Check correct input element name of operation PullData
				var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xsd:schema/xsd:element[@name='ReferenceNumber']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct type of part
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:message[contains(@name, '_InputMessage')]/wsdl:part[@element='tns:ReferenceNumber']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct return element name of operation PullData
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xsd:schema/xsd:element[@name='ReferenceNumber']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct type of part
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:message[contains(@name, '_OutputMessage')]/wsdl:part[@element='tns:ReferenceNumber']", nsmgr);

				Assert.IsNotNull(element);
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractCheckWSDLElementsWrapped()
		{
			using (var host = CreateTestHost(typeof(TestServiceWrapped)))
			using (var client = host.CreateClient())
			using (var res = host.CreateRequest("/Service.asmx?wsdl").GetAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();

				var root = new XmlDocument();
				root.LoadXml(response);

				var nsmgr = new XmlNamespaceManager(root.NameTable);
				nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
				nsmgr.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");

				//Check correct input element name of operation PullData
				var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xsd:schema/xsd:element[@name='PullData']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct type of part
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:message/wsdl:part[@element='tns:PullData']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct return element name of operation PullData
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xsd:schema/xsd:element[@name='PullDataResponse']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct type of part
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:message/wsdl:part[@element='tns:PullDataResponse']", nsmgr);

				Assert.IsNotNull(element);
			}
		}

		[TestMethod]
		public async Task Soap11MessageContractCheckWSDLElementsComplexNotWrapped()
		{
			using (var host = CreateTestHost(typeof(TestServiceComplexNotWrapped)))
			using (var client = host.CreateClient())
			using (var res = host.CreateRequest("/Service.asmx?wsdl").GetAsync().Result)
			{
				res.EnsureSuccessStatusCode();

				var response = await res.Content.ReadAsStringAsync();

				var root = new XmlDocument();
				root.LoadXml(response);

				var nsmgr = new XmlNamespaceManager(root.NameTable);
				nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
				nsmgr.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");

				//Check correct input element name of operation PullData
				var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xsd:schema/xsd:element[@name='PostDataBodyMember']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct type of part
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:message/wsdl:part[@element='tns:PostDataBodyMember']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct return element name of operation PullData
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xsd:schema/xsd:element[@name='ReferenceNumber']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct type of part
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:message/wsdl:part[@element='tns:ReferenceNumber']", nsmgr);

				Assert.IsNotNull(element);
			}
		}

		[TestMethod]
		public async Task SoapMessageContractWithAdditionalEnvelopeXmlnsAttributes()
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Header/>
  <soapenv:Body/>
</soapenv:Envelope>
";

			using (var host = CreateTestHost(typeof(TestService)))
			using (var client = host.CreateClient())
			using (var content = new StringContent(body, Encoding.UTF8, "text/xml"))
			using (var res = host.CreateRequest("/ServiceWithAdditionalEnvelopeXmlnsAttributes.asmx").AddHeader("SOAPAction", @"""EmptyRequest""").And(msg => msg.Content = content).PostAsync().Result)
			{
				res.EnsureSuccessStatusCode();
				var stream = await res.Content.ReadAsStreamAsync();
				XmlDocument doc = new XmlDocument();
				doc.Load(stream);
				XmlElement root = doc.DocumentElement;
				Assert.IsTrue(root.HasAttribute("xmlns:arr"));
				Assert.AreEqual(root.GetAttribute("xmlns:arr"), "http://schemas.microsoft.com/2003/10/Serialization/Arrays");
			}
		}

		private TestServer CreateTestHost(Type serviceType)
		{
			var webHostBuilder = new WebHostBuilder()
				.UseStartup<Startup>()
				.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)));
			return new TestServer(webHostBuilder);
		}
	}
}
