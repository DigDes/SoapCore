using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore
{
	public class WebServiceWSDLMapping
	{
		public string UrlOverride { get; set; }
		public string WsdlFile { get; set; }
		public string WSDLFolder { get; set; }
		public string SchemaFolder { get; set; }
	}
}
