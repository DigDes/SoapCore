using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	public partial class SoapDefinition
	{
		public class SoapBinding : IElementWithSpecialTransforms
		{
			private string _namespace;

			[XmlAttribute(AttributeName = "name")]
			public string Name { get; set; }
			[XmlAttribute(AttributeName = "type")]
			public string Type { get; set; }

			[XmlElement(ElementName = "binding")]
			public SoapBindingInfo Binding { get; set; }

			[XmlElement(ElementName = "operation")]
			public List<WsdlOperation> Operations { get; set; }

			public void DeserializeElements(XmlElement element)
			{
				if (element.Name.EndsWith("binding"))
				{
					Binding = new SoapBindingInfo
					{
						Transport = element.GetAttribute("transport")
					};

					_namespace = element.NamespaceURI;
				}
			}

			public class SoapBindingInfo
			{
				[XmlAttribute(AttributeName = "transport")]
				public string Transport { get; set; }
			}
		}
	}
}
