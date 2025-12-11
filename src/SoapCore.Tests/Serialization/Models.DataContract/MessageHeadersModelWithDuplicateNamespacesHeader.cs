using System.Xml;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.DataContract;

[XmlType(AnonymousType = true, Namespace = "MessageWithNamespacesHeaderNamespace")]
public class MessageHeadersModelWithDuplicateNamespacesHeader
{
	[XmlElement(Order = 0)]
	public string From { get; set; }

	[XmlAnyAttribute]
	public XmlAttribute[] SomeOtherAttributes { get; set; }
}
