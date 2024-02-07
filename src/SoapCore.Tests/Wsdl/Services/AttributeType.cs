using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	public class AttributeType
	{
		[XmlAttribute]
		public string StringProperty { get; set; }

		[XmlAttribute]
		public int IntProperty { get; set; }

		[XmlAttribute]
		public int OptionalIntProperty { get; set; }

		public bool ShouldSerializeOptionalIntProperty()
		{
			return OptionalIntProperty != 0;
		}
	}
}
