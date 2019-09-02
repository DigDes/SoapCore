using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using SoapCore.Tests.Wsdl.Services;

namespace SoapCore.Tests.Wsdl
{
	[TestClass]
	public class WsdlTests
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private readonly XNamespace _xmlSchema = "http://www.w3.org/2001/XMLSchema";

		[TestMethod]
		public void CheckTaskReturnMethod()
		{
			string serviceUrl = "http://localhost:5053";
			StartService(typeof(TaskNoReturnService), serviceUrl);
			var wsdl = GetWsdl(serviceUrl);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

		[TestMethod]
		public void CheckDataContractContainsItself()
		{
			string serviceUrl = "http://localhost:5054";
			StartService(typeof(DataContractContainsItselfService), serviceUrl);
			var wsdl = GetWsdl(serviceUrl);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

		[TestMethod]
		public void CheckDataContractCircularReference()
		{
			string serviceUrl = "http://localhost:5055";
			StartService(typeof(DataContractCircularReferenceService), serviceUrl);
			var wsdl = GetWsdl(serviceUrl);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

		[TestMethod]
		public void CheckNullableEnum()
		{
			string serviceUrl = "http://localhost:5056";
			StartService(typeof(NullableEnumService), serviceUrl);
			var wsdl = GetWsdl(serviceUrl);
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
			string serviceUrl = "http://localhost:5057";
			StartService(typeof(NonNullableEnumService), serviceUrl);
			var wsdl = GetWsdl(serviceUrl);
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

		[TestCleanup]
		public void StopServer()
		{
			_cancellationTokenSource.Cancel();
		}

		private string GetWsdl(string serviceUrl, string serviceName = "Service.svc")
		{
			using (var httpClient = new HttpClient())
			{
				return httpClient.GetStringAsync(string.Format("{0}/{1}?wsdl", serviceUrl, serviceName)).Result;
			}
		}

		private void StartService(Type serviceType, string serviceUrl)
		{
			Task.Run(async () =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls(serviceUrl)
					.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)))
					.UseStartup<Startup>()
					.Build();
				await host.RunAsync(_cancellationTokenSource.Token);
			}).Wait(1000);
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
