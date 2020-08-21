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

			wsdl = GetWsdlFromAsmx();
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
		public void CheckServiceKnownTypes()
		{
			StartService(typeof(ServiceKnownTypesService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			var dogElement = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("Dog") == true);
			Assert.IsNotNull(dogElement);

			var catElement = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("Cat") == true);
			Assert.IsNotNull(dogElement);

			var animalElement = GetElements(dogElement, _xmlSchema + "extension").SingleOrDefault(a => a.Attribute("base")?.Value.Equals("tns:Animal") == true);
			Assert.IsNotNull(animalElement);

			animalElement = GetElements(catElement, _xmlSchema + "extension").SingleOrDefault(a => a.Attribute("base")?.Value.Equals("tns:Animal") == true);
			Assert.IsNotNull(animalElement);
		}

		[TestMethod]
		public void CheckAnonymousServiceKnownType()
		{
			StartService(typeof(AnonymousServiceKnownTypesService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			var dogElement = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("Dog") == true);
			Assert.IsNotNull(dogElement);

			var catElement = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("Cat") == true);
			Assert.IsNotNull(dogElement);

			var animalElement = GetElements(dogElement, _xmlSchema + "extension").SingleOrDefault(a => a.Attribute("base")?.Value.Equals("tns:Animal") == true);
			Assert.IsNotNull(animalElement);

			animalElement = GetElements(catElement, _xmlSchema + "extension").SingleOrDefault(a => a.Attribute("base")?.Value.Equals("tns:Animal") == true);
			Assert.IsNotNull(animalElement);

			var squirrelElement = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("Squirrel") == true);
			Assert.IsNotNull(dogElement);
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
		public void CheckValueTypes()
		{
			StartService(typeof(ValueTypeService));
			var wsdl = GetWsdl();
			StopServer();
			var root = XElement.Parse(wsdl);
			var elementsWithEmptyName = GetElements(root, _xmlSchema + "element").Where(x => x.Attribute("name")?.Value == string.Empty);
			elementsWithEmptyName.ShouldBeEmpty();

			var elementsWithEmptyType = GetElements(root, _xmlSchema + "element").Where(x => x.Attribute("type")?.Value == "xs:");
			elementsWithEmptyType.ShouldBeEmpty();

			File.WriteAllText("test.wsdl", wsdl);

			var inputElement = GetElements(root, _xmlSchema + "complexType").Single(x => x.Attribute("name")?.Value == "AnyStructInput");
			var inputAnnotation = inputElement.Descendants(_xmlSchema + "annotation").SingleOrDefault();
			var inputIsValueType = inputAnnotation.Descendants(_xmlSchema + "appinfo").Descendants(XNamespace.Get("http://schemas.microsoft.com/2003/10/Serialization/") + "IsValueType").SingleOrDefault();

			var outputElement = GetElements(root, _xmlSchema + "complexType").Single(x => x.Attribute("name")?.Value == "AnyStructOutput");
			var outputAnnotation = outputElement.Descendants(_xmlSchema + "annotation").SingleOrDefault();
			var outputIsValueType = outputAnnotation.Descendants(_xmlSchema + "appinfo").Descendants(XNamespace.Get("http://schemas.microsoft.com/2003/10/Serialization/") + "IsValueType").SingleOrDefault();

			Assert.IsNotNull(inputIsValueType);
			Assert.AreEqual("true", inputIsValueType.Value);
			Assert.IsNotNull(inputAnnotation);

			Assert.IsNotNull(outputIsValueType);
			Assert.AreEqual("true", outputIsValueType.Value);
			Assert.IsNotNull(outputAnnotation);
		}

		[TestMethod]
		public void CheckPortType()
		{
			StartService(typeof(PortTypeServiceBase.PortTypeService));
			var wsdl = GetWsdl();
			StopServer();

			var root = new XmlDocument();
			root.LoadXml(wsdl);

			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");

			var element = root.SelectSingleNode("/wsdl:definitions/wsdl:portType", nsmgr);

			Assert.IsNotNull(element);
			Assert.AreEqual(element.Attributes["name"]?.Value, nameof(IPortTypeService));
		}

		[TestMethod]
		public void CheckSystemTypes()
		{
			StartService(typeof(SystemTypesService));
			var wsdl = GetWsdl();
			StopServer();

			var root = new XmlDocument();
			root.LoadXml(wsdl);

			var nsmgr = new XmlNamespaceManager(root.NameTable);
			nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
			nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");

			var element = root.SelectSingleNode("/wsdl:definitions/wsdl:types/xs:schema/xs:element[@name='Method']/xs:complexType/xs:sequence/xs:element", nsmgr);

			Assert.IsNotNull(element);
			Assert.AreEqual(element.Attributes["type"]?.Value, "xs:anyURI");
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
		public void CheckInheritance()
		{
			StartService(typeof(InheritanceService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			var childRenamed = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("Dog") == true);
			Assert.IsNotNull(childRenamed);

			var extension = GetElements(childRenamed, _xmlSchema + "extension").SingleOrDefault(a => a.Attribute("base")?.Value.Equals("tns:Animal") == true);
			Assert.IsNotNull(extension);
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
		public void CheckDictionaryTypeDataContract()
		{
			StartService(typeof(DictionaryTypeListService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);

			var dictionaryItems = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("thing") == true);
			Assert.IsNotNull(dictionaryItems);
			Assert.AreEqual("http://schemas.datacontract.org/2004/07/System.Collections.Generic", dictionaryItems.Attribute(XNamespace.Xmlns + "q2").Value);
			Assert.AreEqual("q2:ArrayOfKeyValuePairOfStringString", dictionaryItems.Attribute("type").Value);

			var complexTypeList = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ComplexModelInput") == true);
			Assert.IsNotNull(complexTypeList);

			var myStringElement = GetElements(complexTypeList, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("StringProperty") == true);
			Assert.IsNotNull(myStringElement);
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
		public async Task CheckXmlAnnotatedTypeServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<XmlModelsService>();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager();

			var requestTypeElement = root.XPathSelectElement("//xsd:element[@name='RequestRoot']", nm);
			Assert.IsNotNull(requestTypeElement);

			var reponseTypeElement = root.XPathSelectElement("//xsd:element[@name='ResponseRoot']", nm);
			Assert.IsNotNull(reponseTypeElement);

			var referenceToExistingDynamicType = root.XPathSelectElement("//xsd:complexType[@name='TestResponseType']/xsd:sequence/xsd:element[@name='DataList3' and @type='tns:ArrayOfTestDataTypeData']", nm);
			Assert.IsNotNull(referenceToExistingDynamicType);

			var selfContainedType = root.XPathSelectElement("//xsd:complexType[@name='TestResponseType']/xsd:sequence/xsd:element[@name='Data' and @minOccurs='0'and @maxOccurs='unbounded' and not(@type)]", nm);
			Assert.IsNotNull(selfContainedType);

			var dynamicTypeElement = root.XPathSelectElement("//xsd:complexType[@name='ArrayOfTestDataTypeData']/xsd:sequence/xsd:element[@name='Data']", nm);
			Assert.IsNotNull(dynamicTypeElement);

			var dynamicTypeElement2 = root.XPathSelectElement("//xsd:complexType[@name='ArrayOfTestDataTypeData1']/xsd:sequence/xsd:element[@name='Data2']", nm);
			Assert.IsNotNull(dynamicTypeElement2);

			var propRootAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropRoot']", nm);
			Assert.IsNotNull(propRootAttribute);

			var propIgnoreAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropIgnore']", nm);
			Assert.IsNull(propIgnoreAttribute);

			var propAnonAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropAnonymous']", nm);
			Assert.IsNotNull(propAnonAttribute);
		}

		[TestMethod]
		public async Task CheckMessageHeadersServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<MessageHeadersService>();
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager();

			var stringPropertyElement = root.XPathSelectElement("//xsd:element[@name='ModifiedStringProperty']", nm);
			Assert.IsNotNull(stringPropertyElement);
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

		private async Task<string> GetWsdlFromMetaBodyWriter<T>()
		{
			var service = new ServiceDescription(typeof(T));
			var baseUrl = "http://tempuri.org/";
			var xmlNamespaceManager = Namespaces.CreateDefaultXmlNamespaceManager();
			var bodyWriter = new MetaBodyWriter(service, baseUrl, null, xmlNamespaceManager);
			var encoder = new SoapMessageEncoder(MessageVersion.Soap12WSAddressingAugust2004, System.Text.Encoding.UTF8, XmlDictionaryReaderQuotas.Max, false, true);
			var responseMessage = Message.CreateMessage(encoder.MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, service, null, xmlNamespaceManager);

			using (var memoryStream = new MemoryStream())
			{
				await encoder.WriteMessageAsync(responseMessage, memoryStream);
				memoryStream.Position = 0;

				using (var streamReader = new StreamReader(memoryStream))
				{
					var result = streamReader.ReadToEnd();
					return result;
				}
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
