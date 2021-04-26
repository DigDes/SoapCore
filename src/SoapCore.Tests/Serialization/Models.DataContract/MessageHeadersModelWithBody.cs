using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[MessageContract]
	public class MessageHeadersModelWithBody
	{
		[MessageHeader]
		public string Prop1 { get; set; }

		[MessageHeader]
		public string Prop2 { get; set; }

		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Prop3 { get; set; }

		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Prop4 { get; set; }

		[MessageHeader(MustUnderstand = true)]
		public string Prop5 { get; set; }

		[MessageHeader(MustUnderstand = true)]
		public string Prop6 { get; set; }

		[MessageHeader(MustUnderstand = true, Namespace = "TestHeaderNamespace")]
		public string Prop7 { get; set; }

		[MessageHeader(MustUnderstand = true, Namespace = "TestHeaderNamespace")]
		public string Prop8 { get; set; }

		[MessageBodyMember]
		public string Body1 { get; set; }

		[MessageBodyMember]
		public string Body2 { get; set; }
	}
}
