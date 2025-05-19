using System;
using System.Xml;
using System.Xml.Schema;

namespace SoapCore
{
	public static class Namespaces
	{
#pragma warning disable SA1310 // Field names must not contain underscore
		public const string XMLNS_XSD = XmlSchema.Namespace;
		public const string XMLNS_XSI = XmlSchema.InstanceNamespace;
		public const string WSDL_NS = "http://schemas.xmlsoap.org/wsdl/";
		public const string SOAP11_NS = "http://schemas.xmlsoap.org/wsdl/soap/";
		public const string SOAP12_NS = "http://schemas.xmlsoap.org/wsdl/soap12/";
		public const string ARRAYS_NS = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
		public const string SYSTEM_NS = "http://schemas.datacontract.org/2004/07/System";
		public const string DataContractNamespace = "http://schemas.datacontract.org/2004/07/";
		public const string SERIALIZATION_NS = "http://schemas.microsoft.com/2003/10/Serialization/";
		public const string WSP_NS = "http://schemas.xmlsoap.org/ws/2004/09/policy";
		public const string WSAM_NS = "http://www.w3.org/2007/05/addressing/metadata";
		public const string WSAW_NS = "http://www.w3.org/2006/05/addressing/wsdl";
		public const string SystemData_NS = "http://schemas.datacontract.org/2004/07/System.Data";
		public const string MSC_NS = "http://schemas.microsoft.com/ws/2005/12/wsdl/contract";
		public const string WSU_NS = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
		public const string HTTP_NS = "http://schemas.microsoft.com/ws/06/2004/policy/http";
		public const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";
		public const string SOAP11_ENVELOPE_NS = "http://schemas.xmlsoap.org/soap/envelope/";
		public const string SOAP12_ENVELOPE_NS = "http://www.w3.org/2003/05/soap-envelope";
		public const string MICROSOFT_TYPES = "http://microsoft.com/wsdl/types/";
#pragma warning restore SA1310 // Field names must not contain underscore

		public static string AddNamespaceIfNotAlreadyPresentAndGetPrefix(XmlNamespaceManager xmlNamespaceManager, string preferredPrefix, string uri)
		{
			var existingPrefix = xmlNamespaceManager.LookupPrefix(uri);
			if (existingPrefix == null)
			{
				var localPrefix = preferredPrefix;
				for (int i = 1; ; i++)
				{
					var existingNamespace = xmlNamespaceManager.LookupNamespace(localPrefix);
					if (existingNamespace == null)
					{
						break;
					}

					localPrefix = $"{preferredPrefix}{i}";
				}

				xmlNamespaceManager.AddNamespace(localPrefix, uri);
				return localPrefix;
			}

			return existingPrefix;
		}

		public static XmlNamespaceManager CreateDefaultXmlNamespaceManager(bool addMicrosoftTypesNamespace)
		{
			var xmlNamespaceManager = new XmlNamespaceManager(new NameTable());

			xmlNamespaceManager.AddNamespace("xsd", Namespaces.XMLNS_XSD);
			xmlNamespaceManager.AddNamespace("wsdl", Namespaces.WSDL_NS);
			xmlNamespaceManager.AddNamespace("msc", Namespaces.MSC_NS);
			xmlNamespaceManager.AddNamespace("wsp", Namespaces.WSP_NS);
			xmlNamespaceManager.AddNamespace("wsu", Namespaces.WSU_NS);
			xmlNamespaceManager.AddNamespace("http", Namespaces.HTTP_NS);
			xmlNamespaceManager.AddNamespace("http1", Namespaces.TRANSPORT_SCHEMA);
			xmlNamespaceManager.AddNamespace("soap", Namespaces.SOAP11_NS);
			xmlNamespaceManager.AddNamespace("soap12", Namespaces.SOAP12_NS);
			xmlNamespaceManager.AddNamespace("ser", Namespaces.SERIALIZATION_NS);
			xmlNamespaceManager.AddNamespace("wsam", Namespaces.WSAM_NS);

			if (addMicrosoftTypesNamespace)
			{
				xmlNamespaceManager.AddNamespace("mst", Namespaces.MICROSOFT_TYPES);
			}

			return xmlNamespaceManager;
		}
	}
}
