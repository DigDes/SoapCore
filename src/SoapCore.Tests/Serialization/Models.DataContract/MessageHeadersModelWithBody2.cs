using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	//same as MessageHeadersModelWithBody, simply added namespace in MessageHeaderAttribute for testing
	[MessageContract]
	public class MessageHeadersModelWithBody2
	{
		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Prop2 { get; set; }

		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Prop1 { get; set; }

		[MessageBodyMember]
		public string Body1 { get; set; }

		[MessageBodyMember]
		public string Body2 { get; set; }
	}
}
