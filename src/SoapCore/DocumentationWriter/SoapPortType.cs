using System.Collections.Generic;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	public partial class SoapDefinition
	{
		public class SoapPortType
		{
			[XmlAttribute(AttributeName = "name")]
			public string Name { get; set; }

			[XmlElement(ElementName = "operation")]
			public List<WsdlOperation> Operations { get; set; }
		}
	}
}
