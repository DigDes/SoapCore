using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[MessageContract(WrapperNamespace = "TestNamespace")]
	public class MessageHeadersModelWithNamespace
	{
		[MessageHeader]
		public string Prop1 { get; set; }

		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Prop2 { get; set; }

		[MessageHeader(MustUnderstand = true)]
		public string Prop3 { get; set; }

		[MessageHeader(MustUnderstand = true, Namespace = "TestHeaderNamespace")]
		public string Prop4 { get; set; }
	}
}
