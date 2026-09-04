using System.Xml;
using System.Xml.Serialization;

namespace SoapCore.Tests.MessageContract.Models
{
	// Test type for issue #1161. Contains an [XmlAnyAttribute] XmlAttribute[] member.
	// When the endpoint uses SoapSerializer.XmlSerializer, the response must use XmlSerializer too.
	// DataContractSerializer cannot serialize XmlAttribute[] and throws InvalidDataContractException.
	[XmlType(AnonymousType = true, Namespace = "http://www.ebxml.org/namespaces/messageHeader")]
	public class Issue1161MessageHeader
	{
		[XmlElement(Order = 0)]
		public string From { get; set; }

		[XmlAnyAttribute]
		public XmlAttribute[] SomeOtherAttributes { get; set; }
	}
}
