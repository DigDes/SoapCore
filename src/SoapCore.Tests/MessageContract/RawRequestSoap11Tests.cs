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

				//Check correct element name of operation PullData
				var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xsd:schema/xsd:element[@name='PullData']", nsmgr);
				Assert.IsNotNull(element);

				//Check correct type of part
				element = root.SelectSingleNode("/wsdl:definitions/wsdl:message/wsdl:part[@element='tns:PullData']", nsmgr);
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

				//Check correct element name of operation PullData
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

		private TestServer CreateTestHost(Type serviceType)
		{
			var webHostBuilder = new WebHostBuilder()
				.UseStartup<Startup>()
				.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)));
			return new TestServer(webHostBuilder);
		}
	}
}
