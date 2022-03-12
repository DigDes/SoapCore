using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	public partial class SoapDefinition
	{
		public class SoapService
		{
			[XmlAttribute(AttributeName = "name")]
			public string Name { get; set; }

			[XmlElement(ElementName = "port")]
			public List<SoapServicePort> Ports { get; set; }

			public class SoapServicePort : IElementWithSpecialTransforms
			{
				[XmlAttribute(AttributeName = "name")]
				public string Name { get; set; }
				[XmlAttribute(AttributeName = "binding")]
				public string Binding { get; set; }

				[XmlElement(ElementName = "address")]
				public SoapServicePortAddress Address { get; set; }

				public void DeserializeElements(XmlElement element)
				{
					if (element.Name.EndsWith("address"))
					{
						Address = new SoapServicePortAddress
						{
							Location = element.GetAttribute("location"),
							Namespace = element.NamespaceURI
						};
					}
				}

				public class SoapServicePortAddress
				{
					[XmlAttribute(AttributeName = "location")]
					public string Location { get; set; }

					public string Namespace { get; set; }
				}
			}
		}
	}
}
