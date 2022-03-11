using System.Collections.Generic;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	[XmlRoot(ElementName = "definitions", Namespace = "http://schemas.xmlsoap.org/wsdl/")]
	public partial class SoapDefinition
	{
		[XmlAttribute(AttributeName = "name")]
		public string Name { get; set; }

		[XmlAttribute(AttributeName = "targetNamespace")]
		public string TargetNamespace { get; set; }

		[XmlElement(ElementName = "types")]
		public SoapTypes Types { get; set; }

		[XmlElement(ElementName = "message")]
		public List<SoapMessage> Messages { get; set; }

		[XmlElement(ElementName = "portType")]
		public SoapPortType PortType { get; set; }

		[XmlElement(ElementName = "binding")]
		public List<SoapBinding> Bindings { get; set; }

		[XmlElement(ElementName = "service")]
		public SoapService Service { get; set; }
	}
}
