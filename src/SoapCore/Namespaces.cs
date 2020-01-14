using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore
{
    public static class Namespaces
	{
#pragma warning disable SA1310 // Field names must not contain underscore
		public const string XMLNS_XSD				= "http://www.w3.org/2001/XMLSchema";
		public const string XMLNS_XSI				= "http://www.w3.org/2001/XMLSchema-instance";
		public const string TRANSPORT_SCHEMA		= "http://schemas.xmlsoap.org/soap/http";
		public const string WSDL_NS					= "http://schemas.xmlsoap.org/wsdl/";
		public const string SOAP11_NS				= "http://schemas.xmlsoap.org/wsdl/soap/";
		public const string SOAP12_NS               = "http://schemas.xmlsoap.org/wsdl/soap12/";
		public const string SOAP11_ENVELOPE_NS		= "http://schemas.xmlsoap.org/soap/envelope/";
		public const string SOAP12_ENVELOPE_NS		= "http://www.w3.org/2003/05/soap-envelope";

		public const string ARRAYS_NS				= "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
		public const string SYSTEM_NS				= "http://schemas.datacontract.org/2004/07/System";
		public const string DataContractNamespace	= "http://schemas.datacontract.org/2004/07/";
		public const string SERIALIZATION_NS		= "http://schemas.microsoft.com/2003/10/Serialization/";
		public const string WSP_NS					= "http://schemas.xmlsoap.org/ws/2004/09/policy";
		public const string WSAM_NS					= "http://www.w3.org/2007/05/addressing/metadata";
		public const string SystemData_NS			= "http://schemas.datacontract.org/2004/07/System.Data";

#pragma warning restore SA1310 // Field names must not contain underscore
	}
}
