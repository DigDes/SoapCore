using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using SoapCore.MessageEncoder;
using SoapCore.Meta;
using SoapCore.ServiceModel;
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
		public void CheckStructsInList()
		{
			StartService(typeof(StructService));
			var wsdl = GetWsdl();
			StopServer();
			var root = XElement.Parse(wsdl);
			var elementsWithEmptyName = GetElements(root, _xmlSchema + "element").Where(x => x.Attribute("name")?.Value == string.Empty);
			elementsWithEmptyName.ShouldBeEmpty();

			var elementsWithEmptyType = GetElements(root, _xmlSchema + "element").Where(x => x.Attribute("type")?.Value == "xs:");
			elementsWithEmptyType.ShouldBeEmpty();

			var structTypeElement = GetElements(root, _xmlSchema + "complexType").Single(x => x.Attribute("name")?.Value == "AnyStruct");
			var annotationNode = structTypeElement.Descendants(_xmlSchema + "annotation").SingleOrDefault();
			var isValueTypeElement = annotationNode.Descendants(_xmlSchema + "appinfo").Descendants(XNamespace.Get("http://schemas.microsoft.com/2003/10/Serialization/") + "IsValueType").SingleOrDefault();
			Assert.IsNotNull(isValueTypeElement);
			Assert.AreEqual("true", isValueTypeElement.Value);
			Assert.IsNotNull(annotationNode);
		}

		[TestMethod]
		public void CheckStreamDeclaration()
		{
			StartService(typeof(StreamService));
			var wsdl = GetWsdl();
			StopServer();
			var root = new XmlDocument();
			root.LoadXml(wsdl);

			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");

			var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xs:schema/xs:element[@name='GetStreamResponse']/xs:complexType/xs:sequence/xs:element", nsmgr);

			Assert.IsNotNull(element);
			Assert.AreEqual("StreamBody", element.Attributes["name"].Value);
			Assert.AreEqual("xs:base64Binary", element.Attributes["type"].Value);
		}

		[TestMethod]
		public void CheckDataContractName()
		{
			StartService(typeof(DataContractNameService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			var childRenamed = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ChildRenamed") == true);
			Assert.IsNotNull(childRenamed);

			var extension = GetElements(childRenamed, _xmlSchema + "extension").SingleOrDefault(a => a.Attribute("base")?.Value.Equals("tns:BaseRenamed") == true);
			Assert.IsNotNull(extension);
		}

		[TestMethod]
		public void CheckEnumList()
		{
			StartService(typeof(EnumListService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			var listResponse = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ListResult") == true);
			Assert.IsNotNull(listResponse);
			Assert.AreEqual("q1:ArrayOfTestEnum", listResponse.Attribute("type").Value);

			var arrayOfTestEnum = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ArrayOfTestEnum") == true);
			Assert.IsNotNull(arrayOfTestEnum);

			var element = GetElements(arrayOfTestEnum, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("TestEnum") == true);
			Assert.IsNotNull(element);
			Assert.AreEqual("0", element.Attribute("minOccurs").Value);
			Assert.AreEqual("unbounded", element.Attribute("maxOccurs").Value);
		}

		[TestMethod]
		public void CheckCollectionDataContract()
		{
			StartService(typeof(CollectionDataContractService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);

			var listStringsResult = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ListStringsResult") == true);
			Assert.IsNotNull(listStringsResult);
			Assert.AreEqual("http://testnamespace.org", listStringsResult.Attribute(XNamespace.Xmlns + "q1").Value);
			Assert.AreEqual("q1:MystringList", listStringsResult.Attribute("type").Value);

			var myStringList = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("MystringList") == true);
			Assert.IsNotNull(myStringList);

			var myStringElement = GetElements(myStringList, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("MyItem") == true);
			Assert.IsNotNull(myStringElement);

			var listMyTypesResult = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ListMyTypesResult") == true);
			Assert.IsNotNull(listMyTypesResult);
			Assert.AreEqual("http://testnamespace.org", listMyTypesResult.Attribute(XNamespace.Xmlns + "q2").Value);
			Assert.AreEqual("q2:MyMyTypeList", listMyTypesResult.Attribute("type").Value);

			var myMyTypeList = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("MyMyTypeList") == true);
			Assert.IsNotNull(myMyTypeList);

			var myMyTypeElement = GetElements(myMyTypeList, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("MyItem") == true);
			Assert.IsNotNull(myMyTypeElement);
		}

		[TestMethod]
		public async Task CheckStringArrayNameWsdl()
		{
			//StartService(typeof(StringListService));
			//var wsdl = GetWsdl();
			//StopServer();
			var wsdl = await GetWsdlFromMetaBodyWriter<StringListService>();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			var root = XElement.Parse(wsdl);

			// Check complexType exists for xmlserializer meta
			var testResultElement = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("type") != null && a.Attribute("name")?.Value.Equals("TestResult") == true);
			Assert.IsNotNull(testResultElement);

			// Now check if we can match the array type up with it's decleration
			var split = testResultElement.Attribute("type").Value.Split(':');
			var typeNamespace = testResultElement.GetNamespaceOfPrefix(split[0]);

			var matchingSchema = GetElements(root, _xmlSchema + "schema").Where(schema => schema.Attribute("targetNamespace")?.Value.Equals(typeNamespace.NamespaceName) == true);
			Assert.IsTrue(matchingSchema.Count() > 0);

			var matched = false;
			foreach (var schema in matchingSchema)
			{
				var matchingElement = GetElements(schema, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals(split[1]) == true);
				if (matchingElement != null)
				{
					matched = true;
				}
			}

			Assert.IsTrue(matched);
		}

		[TestMethod]
		public async Task CheckDateTimeOffsetServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<DateTimeOffsetService>();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
		}

		[TestMethod]
		public async Task CheckXmlSchemaProviderTypeServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<XmlSchemaProviderTypeService>();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
		}

		[TestMethod]
		public async Task CheckTestMultipleTypesServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<TestMultipleTypesService>();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
		}

		[TestMethod]
		public async Task CheckXmlAnonymousTypeServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<XmlModelsService>();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var propRootAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropRoot']", Namespaces.CreateDefaultXmlNamespaceManager());
			Assert.IsNotNull(propRootAttribute);

			var propIgnoreAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropIgnore']", Namespaces.CreateDefaultXmlNamespaceManager());
			Assert.IsNull(propIgnoreAttribute);

			var propAnonAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropAnonymous']", Namespaces.CreateDefaultXmlNamespaceManager());
			Assert.IsNotNull(propAnonAttribute);
		}

		[TestCleanup]
		public void StopServer()
		{
			_host?.StopAsync();
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

		private async Task<string> GetWsdlFromMetaBodyWriter<T>()
		{
			var service = new ServiceDescription(typeof(T));
			var baseUrl = "http://tempuri.org/";
			var xmlNamespaceManager = Namespaces.CreateDefaultXmlNamespaceManager();
			var bodyWriter = new MetaBodyWriter(service, baseUrl, null, xmlNamespaceManager);
			var encoder = new SoapMessageEncoder(MessageVersion.Soap12WSAddressingAugust2004, System.Text.Encoding.UTF8, XmlDictionaryReaderQuotas.Max, false, true);
			var responseMessage = Message.CreateMessage(encoder.MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, service, null, xmlNamespaceManager);

			var memoryStream = new MemoryStream();
			await encoder.WriteMessageAsync(responseMessage, memoryStream);
			memoryStream.Position = 0;

			var streamReader = new StreamReader(memoryStream);
			var result = streamReader.ReadToEnd();
			return result;
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
