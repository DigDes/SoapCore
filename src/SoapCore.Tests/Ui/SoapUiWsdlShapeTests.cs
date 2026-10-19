using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests.Ui
{
	// Contract tests: they pin the WSDL shapes the explorer's JavaScript parser (wsdl-parser.js) relies on,
	// for both serializers, because CI cannot run the JavaScript itself.
	[TestClass]
	public class SoapUiWsdlShapeTests
	{
		private const string SerializationNamespacePrefix = "http://schemas.microsoft.com/2003/10/Serialization";

		private static readonly XNamespace Wsdl = "http://schemas.xmlsoap.org/wsdl/";
		private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";
		private static readonly XNamespace Soap11Binding = "http://schemas.xmlsoap.org/wsdl/soap/";
		private static readonly XNamespace Soap12Binding = "http://schemas.xmlsoap.org/wsdl/soap12/";

		[TestMethod]
		public async Task Wsdl_DataContract_HasTheShapesTheParserReliesOn()
		{
			using var host = CreateHost();

			var wsdl = await GetWsdlAsync(host, "/Shape.svc");

			AssertParserShapes(wsdl, "Get", "urn:ui-shape");
		}

		[TestMethod]
		public async Task Wsdl_XmlSerializer_HasTheParserShapes()
		{
			using var host = CreateHost();

			var wsdl = await GetWsdlAsync(host, "/Shape.asmx");

			AssertParserShapes(wsdl, "Get", "urn:ui-shape");
		}

		[TestMethod]
		public async Task Wsdl_MultiVersionEndpoint_HasOneBindingPerVersionWithMatchingSoapActions()
		{
			using var host = CreateHost();
			var wsdl = await GetWsdlAsync(host, "/Ui.svc");
			using var catalog = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);
			var service = catalog.RootElement.GetProperty("services").EnumerateArray().Single(item => item.GetProperty("path").GetString() == "/Ui.svc");

			// The soap / soap12 binding namespaces in the WSDL are exactly the versions the JSON reports.
			var expectedNamespaces = UiTestHost.Strings(service.GetProperty("soapVersions"))
				.Select(version => version == "1.1" ? Soap11Binding.NamespaceName : Soap12Binding.NamespaceName)
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToArray();
			var bindingOperations = wsdl.Descendants(Wsdl + "binding")
				.SelectMany(binding => binding.Elements(Wsdl + "operation"))
				.ToList();
			var actualNamespaces = bindingOperations
				.SelectMany(operation => operation.Elements())
				.Where(element => element.Name.LocalName == "operation" && (element.Name.Namespace == Soap11Binding || element.Name.Namespace == Soap12Binding))
				.Select(element => element.Name.Namespace.NamespaceName)
				.Distinct()
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToArray();

			Assert.AreEqual(2, expectedNamespaces.Length, "The /Ui.svc endpoint exposes SOAP 1.1 and 1.2.");
			CollectionAssert.AreEqual(expectedNamespaces, actualNamespaces);

			// Every operation of every binding carries the same SOAPAction the JSON reports.
			Assert.IsTrue(bindingOperations.Count > 0, "The WSDL has no binding operations.");
			foreach (var operation in bindingOperations)
			{
				var name = (string)operation.Attribute("name");
				var soapOperation = operation.Elements().Single(element => element.Name.LocalName == "operation" && (element.Name.Namespace == Soap11Binding || element.Name.Namespace == Soap12Binding));
				var expectedAction = UiTestHost.GetOperation(service, name).GetProperty("soapAction").GetString();

				Assert.AreEqual(expectedAction, (string)soapOperation.Attribute("soapAction"), "SOAPAction of operation " + name);
			}
		}

		private static TestServer CreateHost()
		{
			return UiTestHost.CreateWithUi(endpoints =>
			{
				endpoints.UseSoapEndpoint<UiShapeService>("/Shape.svc", UiTestHost.Soap11And12(), SoapSerializer.DataContractSerializer);
				endpoints.UseSoapEndpoint<UiShapeService>("/Shape.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
				endpoints.UseSoapEndpoint<UiTestService>("/Ui.svc", UiTestHost.Soap11And12(), SoapSerializer.DataContractSerializer);
			});
		}

		private static async Task<XDocument> GetWsdlAsync(TestServer host, string path)
		{
			using var response = await UiTestHost.GetRawAsync(host, path + "?wsdl");

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET " + path + "?wsdl");

			return XDocument.Parse(await response.Content.ReadAsStringAsync());
		}

		// Resolves a QName attribute value with the namespace declarations in scope of the element that carries it.
		private static XName ResolveQName(XElement element, string value)
		{
			var colon = value.IndexOf(':');
			if (colon < 0)
			{
				return element.GetDefaultNamespace() + value;
			}

			var prefix = value.Substring(0, colon);
			var ns = element.GetNamespaceOfPrefix(prefix);
			Assert.IsNotNull(ns, "The prefix '" + prefix + "' of '" + value + "' is not declared.");

			return ns + value.Substring(colon + 1);
		}

		private static void AssertParserShapes(XDocument wsdl, string operationName, string contractNamespace)
		{
			// S1: the root is wsdl:definitions and there is at least one schema under wsdl:types.
			Assert.AreEqual(Wsdl + "definitions", wsdl.Root.Name);
			var schemas = wsdl.Root.Elements(Wsdl + "types").Elements(Xs + "schema").ToList();
			Assert.IsTrue(schemas.Count > 0, "The WSDL has no xs:schema under wsdl:types.");

			var declaredElements = new HashSet<XName>();
			var declaredTypes = new HashSet<XName>();
			foreach (var schema in schemas)
			{
				var targetNamespace = (string)schema.Attribute("targetNamespace") ?? string.Empty;
				foreach (var declaration in schema.Elements())
				{
					var declarationName = (string)declaration.Attribute("name");
					if (declarationName == null)
					{
						continue;
					}

					if (declaration.Name == Xs + "element")
					{
						declaredElements.Add(XName.Get(declarationName, targetNamespace));
					}
					else if (declaration.Name == Xs + "complexType" || declaration.Name == Xs + "simpleType")
					{
						declaredTypes.Add(XName.Get(declarationName, targetNamespace));
					}
				}
			}

			// S2: every wsdl:part names a global element (document/literal wrapped), and that element exists in a schema.
			var parts = wsdl.Descendants(Wsdl + "part").ToList();
			Assert.IsTrue(parts.Count > 0, "The WSDL has no wsdl:part.");
			foreach (var part in parts)
			{
				var elementAttribute = part.Attribute("element");
				Assert.IsNotNull(elementAttribute, "wsdl:part '" + (string)part.Attribute("name") + "' has no element attribute (rpc/encoded is not supported by the parser).");
				var elementName = ResolveQName(part, elementAttribute.Value);
				Assert.IsTrue(declaredElements.Contains(elementName), "wsdl:part references an undeclared element: " + elementName);
			}

			// S3: every type, base and ref resolves to the XSD namespace, a declared global type or element,
			// or the DataContract serialization namespace (known SoapCore behaviour: the parser samples those by local name).
			foreach (var element in schemas.SelectMany(schema => schema.Descendants()))
			{
				if (element.Name == Xs + "element")
				{
					AssertResolves(element, "type", declaredTypes);
					AssertResolves(element, "ref", declaredElements);
				}
				else if (element.Name == Xs + "attribute")
				{
					AssertResolves(element, "type", declaredTypes);
				}
				else if (element.Name == Xs + "extension" || element.Name == Xs + "restriction")
				{
					AssertResolves(element, "base", declaredTypes);
				}
			}

			// S4 (fallback pin): the request wrapper element of the operation is a global element
			// in the contract namespace, which is what the JSON reports as contractNamespace.
			Assert.IsTrue(declaredElements.Contains(XName.Get(operationName, contractNamespace)), "There is no global element {" + contractNamespace + "}" + operationName + ".");
		}

		private static void AssertResolves(XElement owner, string attributeName, HashSet<XName> declared)
		{
			var attribute = owner.Attribute(attributeName);
			if (attribute == null)
			{
				return;
			}

			var name = ResolveQName(owner, attribute.Value);
			if (name.NamespaceName == Xs.NamespaceName || name.NamespaceName.StartsWith(SerializationNamespacePrefix, StringComparison.Ordinal))
			{
				return;
			}

			Assert.IsTrue(declared.Contains(name), owner.Name.LocalName + "@" + attributeName + " references an undeclared name: " + name);
		}
	}
}
