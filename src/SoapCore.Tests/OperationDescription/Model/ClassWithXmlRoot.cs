using System.Xml.Serialization;

namespace SoapCore.Tests.OperationDescription.Model
{
	[XmlRoot(ElementName = "test")]
	public class ClassWithXmlRoot
	{
		public string SomeString { get; set; }
	}
}
