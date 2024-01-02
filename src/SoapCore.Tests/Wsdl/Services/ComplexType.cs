using System;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	public class ComplexType
	{
		public int IntProperty { get; set; }
		[XmlElement(ElementName = "stringprop")]
		public string StringProperty { get; set; }
		[XmlElement(ElementName = "mybytes")]
		public byte[] ByteArrayProperty { get; set; }

		public Guid MyGuid { get; set; }
	}
}
