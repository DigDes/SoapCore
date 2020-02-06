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
		public const string SystemData_NS = "http://schemas.datacontract.org/2004/07/System.Data";
		public const string MSC_NS = "http://schemas.microsoft.com/ws/2005/12/wsdl/contract";
		public const string WSU_NS = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
		public const string HTTP_NS = "http://schemas.microsoft.com/ws/06/2004/policy/http";
		public const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";
		public const string SOAP11_ENVELOPE_NS = "http://schemas.xmlsoap.org/soap/envelope/";
		public const string SOAP12_ENVELOPE_NS = "http://www.w3.org/2003/05/soap-envelope";
#pragma warning restore SA1310 // Field names must not contain underscore

		public static void AddDefaultNamespaces(XmlNamespaceManager xmlNamespaceManager)
		{
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "xsd", Namespaces.XMLNS_XSD);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "wsdl", Namespaces.WSDL_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "msc", Namespaces.MSC_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "wsp", Namespaces.WSP_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "wsu", Namespaces.WSU_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "http", Namespaces.HTTP_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "http", Namespaces.TRANSPORT_SCHEMA);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "soap", Namespaces.SOAP11_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "soap12", Namespaces.SOAP12_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "ser", Namespaces.SERIALIZATION_NS);
			AddNamespaceIfNotAlreadyPresent(xmlNamespaceManager, "wsam", Namespaces.WSAM_NS);
		}

		public static string AddNamespaceIfNotAlreadyPresent(XmlNamespaceManager xmlNamespaceManager, string prefix, string uri)
		{
			var existingPrefix = xmlNamespaceManager.LookupPrefix(uri);
			if (existingPrefix == null)
			{
				xmlNamespaceManager.AddNamespace(prefix, uri);
				return prefix;
			}

			return existingPrefix;
		}

		public static XmlNamespaceManager CreateDefaultXmlNamespaceManager()
		{
			var xmlNamespaceManager = new XmlNamespaceManager(new NameTable());
			AddDefaultNamespaces(xmlNamespaceManager);
			return xmlNamespaceManager;
		}
	}
}
