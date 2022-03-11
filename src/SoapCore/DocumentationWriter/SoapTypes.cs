using System.Collections.Generic;
using System.Xml.Serialization;

namespace SoapCore.DocumentationWriter
{
	public partial class SoapDefinition
	{
		public class SoapTypes
		{
			[XmlElement(ElementName = "schema", Namespace = "http://www.w3.org/2001/XMLSchema")]
			public List<SoapTypeSchema> Schema { get; set; }

			public class SoapTypeSchema
			{
				[XmlAttribute(AttributeName = "elementFormDefault")]
				public string ElementFormDefault { get; set; }
				[XmlAttribute(AttributeName = "targetNamespace")]
				public string TargetNamespace { get; set; }

				[XmlElement(ElementName = "import")]
				public List<SoapTypeSchemaImport> Imports { get; set; }
				[XmlElement(ElementName = "element")]
				public List<SoapTypeSchemaElement> Elements { get; set; }

				[XmlElement(ElementName = "complexType")]
				public List<ComplexType> ComplexTypes { get; set; }
				[XmlElement(ElementName = "simpleType")]
				public List<SimpleType> SimpleTypes { get; set; }

				public class SoapTypeSchemaImport
				{
					[XmlAttribute(AttributeName = "namespace")]
					public string Namespace { get; set; }
				}

				public class SoapTypeSchemaElement
				{
					[XmlElement(ElementName = "complexType")]
					public ComplexType ComplexElementType { get; set; }
					[XmlAttribute(AttributeName = "name")]
					public string Name { get; set; }
				}

				public class SimpleType
				{
					[XmlAttribute(AttributeName = "name")]
					public string Name { get; set; }
					[XmlElement(ElementName = "restriction")]
					public ValueRestriction Restriction { get; set; }

					public class ValueRestriction
					{
						[XmlElement(ElementName = "enumeration")]
						public List<EnumValue> EnumerationValue { get; set; }

						public class EnumValue
						{
							[XmlAttribute(AttributeName = "value")]
							public string Value { get; set; }
						}
					}
				}

				public class ComplexType
				{
					[XmlAttribute(AttributeName = "name")]
					public string Name { get; set; }

					[XmlElement(ElementName = "sequence")]
					public Sequence TypeInformation { get; set; }

					public class Sequence
					{
						[XmlElement(ElementName = "element")]
						public List<SequenceElement> Element { get; set; }

						public class SequenceElement
						{
							[XmlAttribute(AttributeName = "name")]
							public string Name { get; set; }
							[XmlAttribute(AttributeName = "minOccurs")]
							public string MinimumOccurences { get; set; }
							[XmlAttribute(AttributeName = "maxOccurs")]
							public string MaximumOccurences { get; set; }
							[XmlAttribute(AttributeName = "type")]
							public string Type { get; set; }
							[XmlAttribute(AttributeName = "ref")]
							public string Ref { get; set; }
							[XmlAttribute(AttributeName = "nillable")]
							public bool Nullable { get; set; } = false;
							[XmlAttribute(AttributeName = "abstract")]
							public bool Abstract { get; set; } = false;
						}
					}
				}
			}
		}
	}
}
