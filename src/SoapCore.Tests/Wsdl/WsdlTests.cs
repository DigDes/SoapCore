using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Text;
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
		private readonly XNamespace _wsdlSchema = "http://schemas.xmlsoap.org/wsdl/";

		private IWebHost _host;

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		[DataRow(SoapSerializer.DataContractSerializer)]
		public async Task CheckBindingAndPortName(SoapSerializer soapSerializer)
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<TaskNoReturnService>(soapSerializer, "BindingName", "PortName");
			var root = XElement.Parse(wsdl);

			// We should have in the wsdl the definition of a complex type representing the nullable enum
			var bindingElements = GetElements(root, _wsdlSchema + "binding").Where(a => a.Attribute("name")?.Value.Equals("BindingName") == true).ToArray();
			bindingElements.ShouldNotBeEmpty();

			var portElements = GetElements(root, _wsdlSchema + "port").Where(a => a.Attribute("name")?.Value.Equals("PortName") == true).ToArray();
			portElements.ShouldNotBeEmpty();
		}

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer, "_soap")]
		[DataRow(SoapSerializer.DataContractSerializer, "")]
		public async Task CheckDefaultBindingAndPortName(SoapSerializer soapSerializer, string bindingSuffix)
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<TaskNoReturnService>(soapSerializer);
			var root = XElement.Parse(wsdl);

			// We should have in the wsdl the definition of a complex type representing the nullable enum
			var bindingElements = GetElements(root, _wsdlSchema + "binding").Where(a => a.Attribute("name")?.Value.Equals("BasicHttpBinding" + bindingSuffix) == true).ToArray();
			bindingElements.ShouldNotBeEmpty();

			var portElements = GetElements(root, _wsdlSchema + "port").Where(a => a.Attribute("name")?.Value.Equals("BasicHttpBinding" + bindingSuffix) == true).ToArray();
			portElements.ShouldNotBeEmpty();
		}

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
		public void CheckEmptyMembersServiceASMX()
		{
			//This did not work without fixing the MetaBodyWriter
			StartService(typeof(OperationContractEmptyMembersService));
			var wsdl = GetWsdlFromAsmx();
			StopServer();

			var root = XElement.Parse(wsdl);
			Assert.IsNotNull(root);
		}

		[TestMethod]
		public void CheckEmptyMembersService()
		{
			StartService(typeof(OperationContractEmptyMembersService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			Assert.IsNotNull(root);
		}

		[TestMethod]
		public void CheckDataContractWithNonDataMembersService()
		{
			StartService(typeof(DataContractWithNonDataMembersService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			bool nonDataMembersPresent = GetElements(root, _xmlSchema + "element")
				.Where(a => a.Attribute("name")?.Value.Contains("NonSerializable") == true)
				.Any();
			Assert.IsFalse(nonDataMembersPresent);
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
		public void CheckEmptyNamesapce()
		{
			StartService(typeof(EmptyNamespaceService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			Assert.IsNotNull(root);
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
		public void CheckIActionResultInterfaceDataContract()
		{
			StartService(typeof(ActionResultContractService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);

			var iactionReultResponse = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("IActionResultTestResult") == true);
			Assert.IsNotNull(iactionReultResponse);
			Assert.AreEqual("xs:anyType", iactionReultResponse.Attribute("type").Value);

			var actionReultResponse = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ActionResultTestResult") == true);
			Assert.IsNotNull(actionReultResponse);
			Assert.AreEqual("xs:anyType", actionReultResponse.Attribute("type").Value);

			var genericActionReultResponse = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("GenericActionResultTestResult") == true);
			Assert.IsNotNull(genericActionReultResponse);
			Assert.AreEqual("xs:string", genericActionReultResponse.Attribute("type").Value);

			var complexGenericActionReultResponse = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ComplexGenericActionResultTestResult") == true);
			Assert.IsNotNull(complexGenericActionReultResponse);
			Assert.AreEqual("http://schemas.datacontract.org/2004/07/SoapCore.Tests.Model", complexGenericActionReultResponse.Attribute(XNamespace.Xmlns + "q1").Value);
			Assert.AreEqual("q1:ComplexModelInput", complexGenericActionReultResponse.Attribute("type").Value);

			var complexTypeList = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("ComplexModelInput") == true);
			Assert.IsNotNull(complexTypeList);

			var myStringElement = GetElements(complexTypeList, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("StringProperty") == true);
			Assert.IsNotNull(myStringElement);
		}

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		[DataRow(SoapSerializer.DataContractSerializer)]
		public async Task CheckStringArrayNameWsdl(SoapSerializer soapSerializer)
		{
			//StartService(typeof(StringListService));
			//var wsdl = GetWsdl();
			//StopServer();
			var wsdl = await GetWsdlFromMetaBodyWriter<StringListService>(soapSerializer);
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

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		[DataRow(SoapSerializer.DataContractSerializer)]
		public async Task CheckComplexTypeAndOutParameterWsdl(SoapSerializer soapSerializer)
		{
			//StartService(typeof(StringListService));
			//var wsdl = GetWsdl();
			//StopServer();
			var wsdl = await GetWsdlFromMetaBodyWriter<ComplexTypeAndOutParameterService>(soapSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			var root = XElement.Parse(wsdl);

			// Check that method response element exists for xmlserializer meta
			var testReponseElement = GetElements(root, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name").Value == "MethodResponse");
			Assert.IsNotNull(testReponseElement);

			var testComplexType = GetElements(testReponseElement, _xmlSchema + "complexType").SingleOrDefault();
			Assert.IsNotNull(testComplexType);

			var testSequence = GetElements(testComplexType, _xmlSchema + "sequence").SingleOrDefault();
			Assert.IsNotNull(testSequence);

			var testElements = GetElements(testSequence, _xmlSchema + "element").ToArray();
			Assert.AreEqual(2, testElements.Length);

			var testElementMethodResult = testElements.SingleOrDefault(a => a.Attribute("name").Value == "MethodResult");
			var testElementMessage = testElements.SingleOrDefault(a => a.Attribute("name").Value == "message");
			Assert.IsNotNull(testElementMethodResult);
			Assert.IsNotNull(testElementMessage);
		}

		[DataTestMethod]
		public async Task CheckComplexComplexTypeWithCustomXmlNamesWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<ComplexComplexTypeWithCustomXmlNamesService>(SoapSerializer.XmlSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			var root = XElement.Parse(wsdl);

			//loading definition of ComplexComplexType
			var testComplexComplexType = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value == "ComplexComplexType");
			Assert.IsNotNull(testComplexComplexType);

			//checking sequence to be there
			var testSequenceOfComplexComplexType = GetElements(testComplexComplexType, _xmlSchema + "sequence").SingleOrDefault();
			Assert.IsNotNull(testSequenceOfComplexComplexType);

			//checking custom name specified per XmlElementAttribute is used
			var testElementOfComplexComplexType = GetElements(testSequenceOfComplexComplexType, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name").Value == "complex");
			Assert.IsNotNull(testElementOfComplexComplexType);

			//loading definition of ComplexType
			var testComplexType = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value == "ComplexType");
			Assert.IsNotNull(testComplexType);

			//checking sequence to be there
			var testSequenceOfComplexType = GetElements(testComplexType, _xmlSchema + "sequence").SingleOrDefault();
			Assert.IsNotNull(testSequenceOfComplexType);

			//checking custom names specified per XmlElementAttribute are used
			var testElementWithCustomName = GetElements(testSequenceOfComplexType, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name").Value == "stringprop");
			Assert.IsNotNull(testElementWithCustomName);

			testElementWithCustomName = GetElements(testSequenceOfComplexType, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name").Value == "mybytes");
			Assert.IsNotNull(testElementWithCustomName);

			//checking both properties without custom names to use the same names as properties in the ComplexType class
			var testElementWithDefaultName = GetElements(testSequenceOfComplexType, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name").Value == "IntProperty");
			Assert.IsNotNull(testElementWithDefaultName);

			testElementWithDefaultName = GetElements(testSequenceOfComplexType, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name").Value == "MyGuid");
			Assert.IsNotNull(testElementWithDefaultName);
		}

		[DataTestMethod]
		public async Task CheckEnumWithCustomNamesXmlSerializedWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<EnumWithCustomNamesService>(SoapSerializer.XmlSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			var root = XElement.Parse(wsdl);

			//loading definition of EnumWithCustomNames
			var enumWithCustomNamesElement = GetElements(root, _xmlSchema + "simpleType").FirstOrDefault(a => a.Attribute("name")?.Value.Equals("EnumWithCustomNames") == true);
			Assert.IsNotNull(enumWithCustomNamesElement);

			//checking restriction to be there
			var testRestrictionOfEnumWithCustomNames = GetElements(enumWithCustomNamesElement, _xmlSchema + "restriction").SingleOrDefault();
			Assert.IsNotNull(testRestrictionOfEnumWithCustomNames);

			//checking enumeration elements to be there
			var testEnumerationElements = GetElements(testRestrictionOfEnumWithCustomNames, _xmlSchema + "enumeration").ToList();
			Assert.IsNotNull(testEnumerationElements);
			Assert.AreEqual(3, testEnumerationElements.Count);

			//checking custom names specified per XmlEnumAttribute are used
			Assert.IsNotNull(testEnumerationElements.SingleOrDefault(e => e.FirstAttribute?.Value == "F"));
			Assert.IsNotNull(testEnumerationElements.SingleOrDefault(e => e.FirstAttribute?.Value == "S"));

			//checking default name specified by enum member
			Assert.IsNotNull(testEnumerationElements.SingleOrDefault(e => e.FirstAttribute?.Value == "ThirdEnumMember"));
		}

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		public async Task CheckOccuranceOfStringType(SoapSerializer soapSerializer)
		{
			//StartService(typeof(StringListService));
			//var wsdl = GetWsdl();
			//StopServer();
			var wsdl = await GetWsdlFromMetaBodyWriter<ComplexTypeAndOutParameterService>(soapSerializer, useMicrosoftGuid: true);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			var root = XElement.Parse(wsdl);

			// Check that method response element exists for xmlserializer meta
			var testComplexType = GetElements(root, _xmlSchema + "complexType").SingleOrDefault(a => a.Attribute("name")?.Value == "ComplexType");
			Assert.IsNotNull(testComplexType);

			var testSequence = GetElements(testComplexType, _xmlSchema + "sequence").SingleOrDefault();
			Assert.IsNotNull(testSequence);

			var testElements = GetElements(testSequence, _xmlSchema + "element").ToArray();
			var stringprop = testElements.SingleOrDefault(a => a.Attribute("name").Value == "stringprop");
			var byteprop = testElements.SingleOrDefault(a => a.Attribute("name").Value == "mybytes");

			Assert.IsNotNull(stringprop);
			Assert.IsTrue(stringprop.Attribute("minOccurs").Value == "0");
			Assert.IsTrue(stringprop.Attribute("maxOccurs").Value == "1");

			Assert.IsNotNull(byteprop);
			Assert.IsTrue(byteprop.Attribute("minOccurs").Value == "0");
			Assert.IsTrue(byteprop.Attribute("maxOccurs").Value == "1");
		}

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		[DataRow(SoapSerializer.DataContractSerializer)]
		public async Task CheckUnqualifiedMembersService(SoapSerializer soapSerializer)
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<TaskNoReturnService>(soapSerializer, "BindingName", "PortName");
			Trace.TraceInformation(wsdl);

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			bool allNeededAreUnqualified = root.XPathSelectElements($"//xsd:complexType[@name='{nameof(TypeWithUnqualifiedMembers)}' or @name='{nameof(UnqType2)}']/xsd:sequence/xsd:element[contains(@name, 'Unqualified')]", nm)
				.All(x => x.Attribute("form")?.Value.Equals("unqualified") == true);
			Assert.IsTrue(allNeededAreUnqualified);

			bool allNeededAreQualified = root.XPathSelectElements($"//xsd:complexType[@name='{nameof(TypeWithUnqualifiedMembers)}' or @name='{nameof(UnqType2)}']/xsd:sequence/xsd:element[contains(@name, 'Qualified')]", nm)
				.All(x => x.Attribute("form")?.Value.Equals("unqualified") != true);
			Assert.IsTrue(allNeededAreQualified);
		}

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		[DataRow(SoapSerializer.DataContractSerializer)]
		public async Task CheckDateTimeOffsetServiceWsdl(SoapSerializer soapSerializer)
		{
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);
			string systemNs = "http://schemas.datacontract.org/2004/07/System";

			var wsdl = await GetWsdlFromMetaBodyWriter<DateTimeOffsetService>(soapSerializer);
			var root = XElement.Parse(wsdl);
			var responseDateElem = root.XPathSelectElement($"//xsd:element[@name='MethodResponse']/xsd:complexType/xsd:sequence/xsd:element[@name='MethodResult']", nm);
			Assert.IsTrue(responseDateElem.ToString().Contains(systemNs));

			var wsdlWCF = await GetWsdlFromMetaBodyWriter<DateTimeOffsetService>(SoapSerializer.DataContractSerializer);
			var rootWCF = XElement.Parse(wsdlWCF);
			var responseDateElemWCF = rootWCF.XPathSelectElement($"//xsd:element[@name='MethodResponse']/xsd:complexType/xsd:sequence/xsd:element[@name='MethodResult']", nm);
			Assert.IsTrue(responseDateElemWCF.ToString().Contains(systemNs));
			var dayOfYearElem = GetElements(rootWCF, _xmlSchema + "element").SingleOrDefault(a => a.Attribute("name")?.Value.Equals("DayOfYear") == true);
			Assert.IsNull(dayOfYearElem);
		}

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		[DataRow(SoapSerializer.DataContractSerializer)]
		public async Task CheckXmlSchemaProviderTypeServiceWsdl(SoapSerializer soapSerializer)
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<XmlSchemaProviderTypeService>(soapSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			var responseDateElem = root.XPathSelectElement("//xsd:element[@name='GetDateResponse']/xsd:complexType/xsd:sequence/xsd:element[@name='GetDateResult' and contains(@type, ':date')]", nm);
			Assert.IsNotNull(responseDateElem);
		}

		[DataTestMethod]
		[DataRow(SoapSerializer.XmlSerializer)]
		[DataRow(SoapSerializer.DataContractSerializer)]
		public async Task CheckTestMultipleTypesServiceWsdl(SoapSerializer soapSerializer)
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<TestMultipleTypesService>(soapSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
		}

		[TestMethod]
		public async Task CheckArrayServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<ArrayService>(SoapSerializer.XmlSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			var nullableArray = root.XPathSelectElement("//xsd:complexType[@name='ArrayRequest']/xsd:sequence/xsd:element[@name='LongNullableArray' and @type='tns:ArrayOfNullableLong' and @nillable='true']", nm);
			Assert.IsNotNull(nullableArray);

			var array = root.XPathSelectElement("//xsd:complexType[@name='ArrayRequest']/xsd:sequence/xsd:element[@name='LongArray' and @type='tns:ArrayOfLong' and @nillable='true']", nm);
			Assert.IsNotNull(array);

			var arrayArray = root.XPathSelectElement("//xsd:complexType[@name='ArrayRequest']/xsd:sequence/xsd:element[@name='LongArrayArray' and @type='tns:ArrayOfArrayOfLong' and @nillable='true']", nm);
			Assert.IsNotNull(arrayArray);

			var stringListList = root.XPathSelectElement("//xsd:complexType[@name='ArrayRequest']/xsd:sequence/xsd:element[@name='StringListList' and @type='tns:ArrayOfArrayOfString' and @nillable='true']", nm);
			Assert.IsNotNull(stringListList);

			var nullableEnumerable = root.XPathSelectElement("//xsd:complexType[@name='EnumerableResponse']/xsd:sequence/xsd:element[@name='LongNullableEnumerable' and @type='tns:ArrayOfNullableLong' and @nillable='true']", nm);
			Assert.IsNotNull(nullableEnumerable);

			var enumerable = root.XPathSelectElement("//xsd:complexType[@name='EnumerableResponse']/xsd:sequence/xsd:element[@name='LongEnumerable' and @type='tns:ArrayOfLong' and @nillable='true']", nm);
			Assert.IsNotNull(enumerable);

			var enumerableEnumberable = root.XPathSelectElement("//xsd:complexType[@name='EnumerableResponse']/xsd:sequence/xsd:element[@name='LongEnumerableEnumerable' and @type='tns:ArrayOfArrayOfLong' and @nillable='true']", nm);
			Assert.IsNotNull(enumerableEnumberable);

			var stringEnumerableEnumberable = root.XPathSelectElement("//xsd:complexType[@name='EnumerableResponse']/xsd:sequence/xsd:element[@name='StringEnumerableEnumerable' and @type='tns:ArrayOfArrayOfString' and @nillable='true']", nm);
			Assert.IsNotNull(stringEnumerableEnumberable);
		}

		[TestMethod]
		public void CheckFieldMembers()
		{
			StartService(typeof(OperationContractFieldMembersService));
			var wsdl = GetWsdl();
			StopServer();

			var root = XElement.Parse(wsdl);
			int fieldElementsCount = GetElements(root, _xmlSchema + "element")
				.Where(a => a.Attribute("name")?.Value.Contains("FieldMember") == true)
				.Count();
			Assert.AreEqual(5, fieldElementsCount);

			int propElementsCount = GetElements(root, _xmlSchema + "element")
				.Where(a => a.Attribute("name")?.Value.Contains("PropMember") == true)
				.Count();
			Assert.AreEqual(5, propElementsCount);
		}

		[TestMethod]
		public void CheckFieldMembersASMX()
		{
			StartService(typeof(OperationContractFieldMembersServiceWrapped));
			var wsdl = GetWsdlFromAsmx();
			StopServer();

			var root = XElement.Parse(wsdl);
			int fieldElementsCount = GetElements(root, _xmlSchema + "element")
				.Where(a => a.Attribute("name")?.Value.Contains("FieldMember") == true)
				.Count();
			Assert.AreEqual(5, fieldElementsCount);

			int propElementsCount = GetElements(root, _xmlSchema + "element")
				.Where(a => a.Attribute("name")?.Value.Contains("PropMember") == true)
				.Count();
			Assert.AreEqual(5, propElementsCount);
		}

		[DataTestMethod]
		public async Task CheckXmlAnnotatedTypeServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<XmlModelsService>(SoapSerializer.XmlSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			var requestTypeElement = root.XPathSelectElement("//xsd:element[@name='RequestRoot']", nm);
			Assert.IsNotNull(requestTypeElement);

			var reponseTypeElement = root.XPathSelectElement("//xsd:element[@name='ResponseRoot']", nm);
			Assert.IsNotNull(reponseTypeElement);

			var referenceToExistingDynamicType = root.XPathSelectElement("//xsd:complexType[@name='TestResponseType']/xsd:sequence/xsd:element[@name='DataList3' and @type='tns:ArrayOfTestDataTypeData']", nm);
			Assert.IsNotNull(referenceToExistingDynamicType);

			var selfContainedType = root.XPathSelectElement("//xsd:complexType[@name='TestResponseType']/xsd:sequence/xsd:element[@name='Data3' and @minOccurs='0'and @maxOccurs='unbounded' and not(@type)]", nm);
			Assert.IsNotNull(selfContainedType);

			var dynamicTypeElement = root.XPathSelectElement("//xsd:complexType[@name='ArrayOfTestDataTypeData']/xsd:sequence/xsd:element[@name='Data']", nm);
			Assert.IsNotNull(dynamicTypeElement);

			var dynamicTypeElement2 = root.XPathSelectElement("//xsd:complexType[@name='ArrayOfTestDataTypeData1']/xsd:sequence/xsd:element[@name='Data2']", nm);
			Assert.IsNotNull(dynamicTypeElement2);

			var choiceTypeElement = root.XPathSelectElement("//xsd:complexType[@name='TestResponseType']/xsd:sequence/xsd:choice[@minOccurs='0'and @maxOccurs='unbounded']/xsd:element[@name='Data4']", nm);
			Assert.IsNotNull(choiceTypeElement);

			var choiceTypeElement2 = root.XPathSelectElement("//xsd:complexType[@name='TestResponseType']/xsd:sequence/xsd:choice[@minOccurs='0'and @maxOccurs='unbounded']/xsd:element[@name='Data5']", nm);
			Assert.IsNotNull(choiceTypeElement2);

			var propRootAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropRoot']", nm);
			Assert.IsNotNull(propRootAttribute);

			var propIgnoreAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropIgnore']", nm);
			Assert.IsNull(propIgnoreAttribute);

			var propAnonAttribute = root.XPathSelectElement("//xsd:attribute[@name='PropAnonymous']", nm);
			Assert.IsNotNull(propAnonAttribute);
		}

		[DataTestMethod]
		public async Task CheckXmlAnnotatedChoiceReturnServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<XmlAnnotatedChoiceReturnService>(SoapSerializer.XmlSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			var requestTypeElement = root.XPathSelectElement("//xsd:element[@name='GetResponseResponse']", nm);
			Assert.IsNotNull(requestTypeElement);

			var choiceElement = root.XPathSelectElement("//xsd:element[@name='GetResponseResponse']/xsd:complexType/xsd:sequence/xsd:choice", nm);
			Assert.IsNotNull(choiceElement);

			var resultResponseElement = root.XPathSelectElement("//xsd:element[@name='GetResponseResponse']/xsd:complexType/xsd:sequence/xsd:choice/xsd:element[@name='resultResp']", nm);
			Assert.IsNotNull(resultResponseElement);

			var integerElement = root.XPathSelectElement("//xsd:element[@name='GetResponseResponse']/xsd:complexType/xsd:sequence/xsd:choice/xsd:element[@name='integerNumber']", nm);
			Assert.IsNotNull(integerElement);

			var choiceComplexTypeElement = root.XPathSelectElement("//xsd:complexType[@name='ResultResponse']", nm);
			Assert.IsNotNull(choiceComplexTypeElement);

			Assert.IsNotNull(choiceComplexTypeElement.XPathSelectElement("//xsd:complexType/xsd:sequence/xsd:choice/xsd:element[@name='first' and @type='xsd:int']", nm));
			Assert.IsNotNull(choiceComplexTypeElement.XPathSelectElement("//xsd:complexType/xsd:sequence/xsd:choice/xsd:element[@name='second' and @type='xsd:string']", nm));
		}

		[DataTestMethod]
		public async Task CheckMessageHeadersServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<MessageHeadersService>(SoapSerializer.XmlSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			var stringPropertyElement = root.XPathSelectElement("//xsd:element[@name='ModifiedStringProperty']", nm);
			Assert.IsNotNull(stringPropertyElement);
		}

		[TestMethod]
		public async Task CheckDefaultValueAttributesServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<DefaultValueAttributesService>(SoapSerializer.XmlSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			var booleanWithNoDefaultPropertyElement = root.XPathSelectElement("//xsd:element[@name='BooleanWithNoDefaultProperty' and @minOccurs='1' and @maxOccurs='1' and not(@default)]", nm);
			Assert.IsNotNull(booleanWithNoDefaultPropertyElement);

			var booleanWithDefaultNullPropertyElement = root.XPathSelectElement("//xsd:element[@name='BooleanWithDefaultNullProperty' and @minOccurs='1' and @maxOccurs='1' and not(@default)]", nm);
			Assert.IsNotNull(booleanWithDefaultNullPropertyElement);

			var booleanWithDefaultFalsePropertyElement = root.XPathSelectElement("//xsd:element[@name='BooleanWithDefaultFalseProperty' and @minOccurs='0' and @maxOccurs='1' and @default='false']", nm);
			Assert.IsNotNull(booleanWithDefaultFalsePropertyElement);

			var booleanWithDefaultTruePropertyElement = root.XPathSelectElement("//xsd:element[@name='BooleanWithDefaultTrueProperty' and @minOccurs='0' and @maxOccurs='1' and @default='true']", nm);
			Assert.IsNotNull(booleanWithDefaultTruePropertyElement);

			var intWithNoDefaultPropertyElement = root.XPathSelectElement("//xsd:element[@name='IntWithNoDefaultProperty' and @minOccurs='1' and @maxOccurs='1' and not(@default)]", nm);
			Assert.IsNotNull(intWithNoDefaultPropertyElement);

			var intWithDefaultPropertyElement = root.XPathSelectElement("//xsd:element[@name='IntWithDefaultProperty' and @minOccurs='0' and @maxOccurs='1' and @default='42']", nm);
			Assert.IsNotNull(intWithDefaultPropertyElement);

			var stringWithNoDefaultPropertyElement = root.XPathSelectElement("//xsd:element[@name='StringWithNoDefaultProperty' and @minOccurs='0' and @maxOccurs='1' and not(@default)]", nm);
			Assert.IsNotNull(stringWithNoDefaultPropertyElement);

			var stringWithDefaultNullPropertyElement = root.XPathSelectElement("//xsd:element[@name='StringWithDefaultNullProperty' and @minOccurs='0' and @maxOccurs='1' and not(@default)]", nm);
			Assert.IsNotNull(stringWithDefaultNullPropertyElement);

			var stringWithDefaultPropertyElement = root.XPathSelectElement("//xsd:element[@name='StringWithDefaultProperty' and @minOccurs='0' and @maxOccurs='1' and @default='default']", nm);
			Assert.IsNotNull(stringWithDefaultPropertyElement);
		}

		[TestMethod]
		public async Task CheckDataContractKnownTypeAttributeServiceWsdl()
		{
			var wsdl = await GetWsdlFromMetaBodyWriter<TestService>(SoapSerializer.DataContractSerializer);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);

			Assert.IsFalse(wsdl.Contains("name=\"\""));

			var root = XElement.Parse(wsdl);
			var nm = Namespaces.CreateDefaultXmlNamespaceManager(false);

			var schemaElement = root.XPathSelectElement("//xsd:schema[@targetNamespace='http://schemas.datacontract.org/2004/07/SoapCore.Tests.Model']", nm);
			Assert.IsNotNull(schemaElement);

			Assert.IsNotNull(schemaElement.XPathSelectElement("//xsd:complexType[@name='ComplexInheritanceModelInputA']/xsd:complexContent/xsd:extension[@base='tns:ComplexInheritanceModelInputBase']", nm));
			Assert.IsNotNull(schemaElement.XPathSelectElement("//xsd:element[@name='ComplexInheritanceModelInputA' and @type='tns:ComplexInheritanceModelInputA']", nm));
			Assert.IsNotNull(schemaElement.XPathSelectElement("//xsd:complexType[@name='ComplexInheritanceModelInputB']/xsd:complexContent/xsd:extension[@base='tns:ComplexInheritanceModelInputA']", nm));
			Assert.IsNotNull(schemaElement.XPathSelectElement("//xsd:element[@name='ComplexInheritanceModelInputB' and @type='tns:ComplexInheritanceModelInputB']", nm));
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

		private async Task<string> GetWsdlFromMetaBodyWriter<T>(SoapSerializer serializer, string bindingName = null, string portName = null, bool useMicrosoftGuid = false)
		{
			var service = new ServiceDescription(typeof(T));
			var baseUrl = "http://tempuri.org/";
			var xmlNamespaceManager = Namespaces.CreateDefaultXmlNamespaceManager(useMicrosoftGuid);
			var defaultBindingName = !string.IsNullOrWhiteSpace(bindingName) ? bindingName : "BasicHttpBinding";
			var bodyWriter = serializer == SoapSerializer.DataContractSerializer
				? new MetaWCFBodyWriter(service, baseUrl, defaultBindingName, false, new[] { new SoapBindingInfo(MessageVersion.None, bindingName, portName) }) as BodyWriter
				: new MetaBodyWriter(service, baseUrl, xmlNamespaceManager, defaultBindingName, new[] { new SoapBindingInfo(MessageVersion.None, bindingName, portName) }, useMicrosoftGuid) as BodyWriter;
			var encoder = new SoapMessageEncoder(MessageVersion.Soap12WSAddressingAugust2004, Encoding.UTF8, false, XmlDictionaryReaderQuotas.Max, false, true, false, null, bindingName, portName);
			var responseMessage = Message.CreateMessage(encoder.MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, service, xmlNamespaceManager, defaultBindingName, false);

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
			_host = new WebHostBuilder()
				.UseKestrel()
				.UseUrls("http://127.0.0.1:0")
				.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)))
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
