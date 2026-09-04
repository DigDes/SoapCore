using System.ServiceModel;

namespace SoapCore.Tests.MessageContract.Models
{
	[MessageContract]
	public class Issue1161MessageContract
	{
		[MessageHeader(Name = "MessageHeader", Namespace = "http://www.ebxml.org/namespaces/messageHeader")]
		public Issue1161MessageHeader Header { get; set; }

		[MessageBodyMember]
		public string BodyMessage { get; set; }
	}
}
