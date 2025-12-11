using System.ServiceModel;
using System.Xml;
using System.Xml.Serialization;

[MessageContract]
public class MyMessage {
    private string _body;

    [MessageHeader(Name = "MessageHeader", Namespace = "http://www.ebxml.org/namespaces/messageHeader")]
    public MyMessageHeader Header { get; set; }

    [MessageBodyMember]
    public string BodyMessage
    {
        get => _body;
        set => _body = value;
    }
}


[XmlType(AnonymousType = true, Namespace = "http://www.ebxml.org/namespaces/messageHeader")]
public class MyMessageHeader
{
    private string _from;
    private XmlAttribute[] _anyAttr;

    [XmlElement(Order = 0)]
    public string From
    {
        get => _from;
        set => _from = value;
    }

    [XmlAnyAttribute]
    public XmlAttribute[] SomeOtherAttributes
    {
        get => _anyAttr;
        set => _anyAttr = value;
    }
}
