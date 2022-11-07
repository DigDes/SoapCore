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
	public class WsdlIncludeTests
	{
		private IWebHost _host;

		[TestMethod]
		public void CheckXSDInclude()
		{
			StartService(typeof(EchoIncludeService));
			var wsdl = GetWsdlFromAsmx();
			StopServer();

			var root = new XmlDocument();
			root.LoadXml(wsdl);

			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
			nsmgr.AddNamespace("soapbind", "http://schemas.xmlsoap.org/wsdl/soap/");

			var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xs:schema/xs:include[1]", nsmgr);

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			string url = address + "/Management/Service.asmx?xsd&name=echoInclude.xsd";

			Assert.IsNotNull(element);
			Assert.AreEqual(url, element.Attributes["schemaLocation"]?.Value);
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
		public void CheckXSDIncludeXSD()
		{
			StartService(typeof(EchoIncludeService));
			var xsd = GetXSDFromAsmx();
			StopServer();

			var root = new XmlDocument();
			root.LoadXml(xsd);

			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
			nsmgr.AddNamespace("soapbind", "http://schemas.xmlsoap.org/wsdl/soap/");

			var element = root.SelectSingleNode("/xs:schema/xs:include[1]", nsmgr);

			var addresses = _host.ServerFeatures.Get<IServerAddressesFeature>();
			var address = addresses.Addresses.Single();

			string url = address + "/Service.asmx?xsd&name=echoIncluded.xsd";

			Assert.IsNotNull(element);
			Assert.AreEqual(url, element.Attributes["schemaLocation"]?.Value);
		}

		[TestCleanup]
		public void StopServer()
		{
			_host?.StopAsync();
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
				return httpClient.GetStringAsync(string.Format("{0}/{1}?xsd&name=echoInclude.xsd", address, serviceName)).Result;
			}
		}

		private void StartService(Type serviceType)
		{
			_host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://127.0.0.1:0")
					.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType, "echoInclude.wsdl")))
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
	}
}
