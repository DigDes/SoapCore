using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	public class ComplexComplexType
	{
		[XmlElement(ElementName = "complex")]
		public ComplexType ComplexType { get; set; }
	}
}
