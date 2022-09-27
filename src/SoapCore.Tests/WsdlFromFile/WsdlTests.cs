using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.WsdlFromFile.Services;

namespace SoapCore.Tests.WsdlFromFile
{
	[TestClass]
	public class WsdlTests
	{
		private IWebHost _host;

		[TestMethod]
		public void CheckWSDLExists()
		{
			StartService(typeof(MeasurementSiteTablePublicationService));
			var wsdl = GetWsdlFromAsmx();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();

			var root = new XmlDocument();
			root.LoadXml(wsdl);
			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			var element = root.SelectSingleNode("/wsdl:definitions", nsmgr);
			Assert.IsNotNull(element);
		}

		[TestMethod]
		public void CheckXSDExists()
		{
			StartService(typeof(MeasurementSiteTablePublicationService));
			var xsd = GetXSDFromAsmx();
			Trace.TraceInformation(xsd);
			Assert.IsNotNull(xsd);
			StopServer();

			XmlSchema.Read(new XmlTextReader(new StringReader(xsd)), ValidationCallback);

			static void ValidationCallback(object sender, ValidationEventArgs args)
			{
				Assert.Fail(args.Message);
			}
		}

		[TestMethod]
		public void CheckAddressLocation()
		{
			StartService(typeof(MeasurementSiteTablePublicationService));
			var wsdl = GetWsdlFromAsmx();
			StopServer();

			var root = new XmlDocument();
			root.LoadXml(wsdl);

			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
			nsmgr.AddNamespace("soapbind", "http://schemas.xmlsoap.org/wsdl/soap/");

			var element = root.SelectSingleNode("/wsdl:definitions/wsdl:service/wsdl:port/soapbind:address", nsmgr);

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			string url = address + "/Management/Service.asmx";
			Assert.IsNotNull(element);
			Assert.AreEqual(element.Attributes["location"]?.Value, url);
		}

		[TestMethod]
		public void CheckXSDImport()
		{
			StartService(typeof(MeasurementSiteTablePublicationService));
			var wsdl = GetWsdlFromAsmx();
			StopServer();

			var root = new XmlDocument();
			root.LoadXml(wsdl);

			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
			nsmgr.AddNamespace("soapbind", "http://schemas.xmlsoap.org/wsdl/soap/");

			var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xs:schema/xs:import[1]", nsmgr);

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			string url = address + "/Management/Service.asmx?xsd&name=DATEXII_3_MessageContainer.xsd";

			Assert.IsNotNull(element);
			Assert.AreEqual(element.Attributes["namespace"]?.Value, "http://datex2.eu/schema/3/messageContainer");
			Assert.AreEqual(element.Attributes["schemaLocation"]?.Value, url);
		}

		[TestCleanup]
		public void StopServer()
		{
			_host?.StopAsync();
		}

		private string GetWsdl()
		{
			var serviceName = "Service.svc";

			return GetWsdlFromService(serviceName);
		}

		private string GetWsdlFromService(string serviceName)
		{
			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			using (var httpClient = new HttpClient())
			{
				return httpClient.GetStringAsync(string.Format("{0}/{1}?wsdl", address, serviceName)).Result;
			}
		}

		private string GetWsdlFromAsmx()
		{
			var serviceName = "Service.asmx";

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			using (var httpClient = new HttpClient())
			{
				return httpClient.GetStringAsync(string.Format("{0}/{1}?wsdl", address, serviceName)).Result;
			}
		}

		private string GetXSDFromAsmx()
		{
			var serviceName = "Service.asmx";

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			using (var httpClient = new HttpClient())
			{
				return httpClient.GetStringAsync(string.Format("{0}/{1}?xsd&name=DATEXII_3_MessageContainer.xsd", address, serviceName)).Result;
			}
		}

		private void StartService(Type serviceType)
		{
			_host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://127.0.0.1:0")
					.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType, "SnapshotPull.wsdl")))
					.UseStartup<Startup>()
					.Build();

			_ = _host.RunAsync();

			//Don't think this is true anymore and can't reproduce the behaviour locally if I remove the code below but not confident enough to remove it...
			//
			//There's a race condition without this check, the host may not have an address immediately and we need to wait for it but the collection
			//may actually be totally empty, All() will be true if the collection is empty.
			while (_host == null || _host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.All(a => a.EndsWith(":0")))
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
