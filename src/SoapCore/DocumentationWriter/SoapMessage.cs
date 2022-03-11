using System.Collections.Generic;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	public partial class SoapDefinition
	{
		public class SoapMessage
		{
			[XmlAttribute(AttributeName = "name")]
			public string Name { get; set; }
			[XmlElement(ElementName = "part")]
			public List<SoapMessagePart> Part { get; set; }

			public class SoapMessagePart
			{
				[XmlAttribute(AttributeName = "name")]
				public string Name { get; set; }
				[XmlAttribute(AttributeName = "element")]
				public string Element { get; set; }
			}
		}
	}
}
