using System.IO;
using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperNamespace = "TestNamespace")]
	public class DataContractWithStream
	{
		[MessageHeader]
		public string Header1 { get; set; }

		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Header2 { get; set; }

		[MessageHeader(MustUnderstand = true)]
		public string Header3 { get; set; }

		[MessageHeader(MustUnderstand = true, Namespace = "TestHeaderNamespace")]
		public string Header4 { get; set; }

		[MessageBodyMember]
		public Stream Data { get; set; }
	}
}
