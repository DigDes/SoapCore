using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(IsWrapped = false)]
	public class UnwrappedStringMessageBodyMemberResponse
	{
		[MessageBodyMember]
		public string StringProperty { get; set; }
	}
}
