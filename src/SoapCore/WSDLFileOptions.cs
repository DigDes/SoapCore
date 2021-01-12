using System.Collections.Generic;
using SoapCore.Meta;

namespace SoapCore
{
	public class WsdlFileOptions
	{
		public Dictionary<string, WebServiceWSDLMapping> WebServiceWSDLMapping { get; set; }
		public string UrlOverride { get; set; }
		public string VirtualPath { get; set; }
		public string AppPath { get; set; }
	}
}
