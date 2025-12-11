using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract;

[MessageContract]
public class MessageHeadersModelWithDuplicateNamespaces
{
	[MessageHeader(Name = "MessageHeader", Namespace = "MessageWithNamespacesHeaderNamespace")]
	public MessageHeadersModelWithDuplicateNamespacesHeader Header { get; set; }

	[MessageBodyMember]
	public string BodyMessage { get; set; }
}
