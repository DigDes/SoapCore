using System;
using System.Collections.Generic;

namespace SoapCore
{
	public class WsdlFileOptions
	{
		public virtual Dictionary<string, WebServiceWSDLMapping> WebServiceWSDLMapping { get; set; } = new Dictionary<string, WebServiceWSDLMapping>();
		public string UrlOverride { get; set; }
		public string VirtualPath { get; set; }
		public string AppPath { get; set; }
	}

	public class WsdlFileOptionsCaseInsensitive : WsdlFileOptions
	{
		public override Dictionary<string, WebServiceWSDLMapping> WebServiceWSDLMapping { get; set; } = new Dictionary<string, WebServiceWSDLMapping>(StringComparer.OrdinalIgnoreCase);
	}
}
