using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[MessageContract]
	public class MessageHeadersModelWithBody
	{
		[MessageHeader]
		public string Prop2 { get; set; }

		[MessageHeader]
		public string Prop1 { get; set; }

		[MessageBodyMember]
		public string Body1 { get; set; }

		[MessageBodyMember]
		public string Body2 { get; set; }
	}
}
