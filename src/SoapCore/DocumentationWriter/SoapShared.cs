using System.Xml;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	public partial class SoapDefinition
	{
		public class WsdlOperation : IElementWithSpecialTransforms
		{
			private string _namespace;

			[XmlAttribute(AttributeName = "name")]
			public string Name { get; set; }

			[XmlElement(ElementName = "input")]
			public OperationItem Input { get; set; }
			[XmlElement(ElementName = "output")]
			public OperationItem Output { get; set; }
			[XmlElement(ElementName = "fault")]
			public OperationItem Fault { get; set; }

			[XmlElement(ElementName = "operation")]
			public SoapOperation Operation { get; set; }

			public void DeserializeElements(XmlElement element)
			{
				if (element.Name.EndsWith("operation"))
				{
					Operation = new SoapOperation
					{
						SoapAction = element.GetAttribute("soapAction"),
						Style = element.GetAttribute("style")
					};

					_namespace = element.NamespaceURI;
				}
			}

			public class OperationItem : IElementWithSpecialTransforms
			{
				[XmlAttribute(AttributeName = "message")]
				public string Message { get; set; }

				[XmlElement(ElementName = "body")]
				public OperationBody Body { get; set; }

				public void DeserializeElements(XmlElement element)
				{
					if (element.Name.EndsWith("body"))
					{
						Body = new OperationBody
						{
							Use = element.GetAttribute("use")
						};
					}
				}

				public class OperationBody
				{
					[XmlAttribute(AttributeName = "use")]
					public string Use { get; set; }
				}
			}
		}

		public class SoapOperation
		{
			[XmlAttribute(AttributeName = "soapAction")]
			public string SoapAction { get; set; }
			[XmlAttribute(AttributeName = "style")]
			public string Style { get; set; }
		}
	}
}
