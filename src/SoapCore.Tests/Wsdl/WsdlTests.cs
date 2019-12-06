using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using SoapCore.Tests.Wsdl.Services;

namespace SoapCore.Tests.Wsdl
{
	[TestClass]
	public class WsdlTests
	{
		private readonly XNamespace _xmlSchema = "http://www.w3.org/2001/XMLSchema";

		private IWebHost _host;

		[TestMethod]
		public void CheckTaskReturnMethod()
		{
			StartService(typeof(TaskNoReturnService));
			var wsdl = GetWsdl();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

		[TestMethod]
		public void CheckDataContractContainsItself()
		{
			StartService(typeof(DataContractContainsItselfService));
			var wsdl = GetWsdl();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

		[TestMethod]
		public void CheckDataContractCircularReference()
		{
			StartService(typeof(DataContractCircularReferenceService));
			var wsdl = GetWsdl();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

		[TestMethod]
		public void CheckNullableEnum()
		{
			StartService(typeof(NullableEnumService));
			var wsdl = GetWsdl();
			StopServer();

			// Parse wsdl content as XML
			var root = XElement.Parse(wsdl);

			// We should have in the wsdl the definition of a complex type representing the nullable enum
			var complexTypeElements = GetElements(root, _xmlSchema + "complexType").Where(a => a.Attribute("name")?.Value.Equals("NullableOfNulEnum") == true).ToList();
			complexTypeElements.ShouldNotBeEmpty();

			// We should have in the wsdl the definition of a simple type representing the enum
			var simpleTypeElements = GetElements(root, _xmlSchema + "simpleType").Where(a => a.Attribute("name")?.Value.Equals("NulEnum") == true).ToList();
			simpleTypeElements.ShouldNotBeEmpty();
		}

		[TestMethod]
		public void CheckNonNullableEnum()
		{
			StartService(typeof(NonNullableEnumService));
			var wsdl = GetWsdl();
			StopServer();

			// Parse wsdl content as XML
			var root = XElement.Parse(wsdl);

			// We should not have in the wsdl any definition of a complex type representing a nullable enum
			var complexTypeElements = GetElements(root, _xmlSchema + "complexType").Where(a => a.Attribute("name")?.Value.Equals("NullableOfNulEnum") == true).ToList();
			complexTypeElements.ShouldBeEmpty();

			// We should have in the wsdl the definition of a simple type representing the enum
			var simpleTypeElements = GetElements(root, _xmlSchema + "simpleType").Where(a => a.Attribute("name")?.Value.Equals("NulEnum") == true).ToList();
			simpleTypeElements.ShouldNotBeEmpty();
		}

		[TestMethod]
		public void CheckStreamDeclaration()
		{
			StartService(typeof(StreamService));
			var wsdl = GetWsdl();
			StopServer();
			var root = new XmlDocument();
			root.LoadXml(wsdl);

			XmlNamespaceManager nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");

			var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xs:schema/xs:element[@name='GetStreamResponse']/xs:complexType/xs:sequence/xs:element", nsmgr);

			Assert.IsNotNull(element);
			Assert.AreEqual("StreamBody", element.Attributes["name"].Value);
			Assert.AreEqual("xs:base64Binary", element.Attributes["type"].Value);
		}

		[TestCleanup]
		public void StopServer()
		{
			_host.StopAsync();
		}

		private string GetWsdl()
		{
			var serviceName = "Service.svc";

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			using (var httpClient = new HttpClient())
			{
				return httpClient.GetStringAsync(string.Format("{0}/{1}?wsdl", address, serviceName)).Result;
			}
		}

		private void StartService(Type serviceType)
		{
			Task.Run(() =>
			{
				_host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://127.0.0.1:0")
					.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)))
					.UseStartup<Startup>()
					.Build();

				_host.Run();
			});

			while (_host == null || _host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First().EndsWith(":0"))
			{
				Thread.Sleep(2000);
			}
		}

		private List<XElement> GetElements(XElement root, XName name)
		{
			var list = new List<XElement>();
			foreach (var xElement in root.Elements())
			{
				if (xElement.Name.Equals(name))
				{
					list.Add(xElement);
				}

				list.AddRange(GetElements(xElement, name));
			}

			return list;
		}
	}
}
