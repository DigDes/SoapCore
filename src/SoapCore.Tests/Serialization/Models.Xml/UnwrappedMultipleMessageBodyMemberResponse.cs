using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(IsWrapped = false)]
	public class UnwrappedMultipleMessageBodyMemberResponse
	{
		[MessageBodyMember(Name = "foo1", Namespace = "http://tempuri.org/NotWrappedPropertyComplexInput")]
		public NotWrappedPropertyComplexInput NotWrappedComplexInput1 { get; set; }

		[MessageBodyMember(Name = "foo2", Namespace = "http://tempuri.org/NotWrappedPropertyComplexInput")]
		public NotWrappedPropertyComplexInput NotWrappedComplexInput2 { get; set; }
	}
}
