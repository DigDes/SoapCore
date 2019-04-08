using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = nameof(PingComplexModelOldStyleResponse), WrapperNamespace = ServiceNamespace.Value, IsWrapped = true)]
	public class PingComplexModelOldStyleResponse
	{
		// similar to return result in new style
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 0)]
		public bool PingComplexModelOldStyleResult { get; set; }

		// pure input value, similar to non-ref param in new style
		// not present in response
		// ComplexModel1 inputModel;

		// ref (in and out) param, present both in request and response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 1)]
		public ComplexModel2 ResponseModelRef1 { get; set; }

		// pure input value, not present in response
		// ComplexObject data1

		// ref (in and out) param, present both in request and response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 2)]
		public ComplexModel1 ResponseModelRef2 { get; set; }

		// pure input value, not present in response
		// ComplexObject data2

		// pure output param, present only in response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 3)]
		public ComplexModel2 ResponseModelOut1 { get; set; }

		// pure output param, present only in response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 4)]
		public ComplexModel1 ResponseModelOut2 { get; set; }

		// pure input value
		// not present in response
		// ComplexObject data2
	}
}
