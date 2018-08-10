using System.ServiceModel;
using System.Runtime.Serialization;

namespace Models
{
	[MessageContract(WrapperName = nameof(PingComplexModelOldStyleResponse), WrapperNamespace = ServiceNamespace.Value, IsWrapped = true)]
	public class PingComplexModelOldStyleResponse
	{
		// similar to return result in new style
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 0)]
		public bool PingComplexModelOldStyleResult;

		// pure input value, similar to non-ref param in new style
		// not present in response
		// ComplexModelInput inputModel;

		// ref (in and out) param, present both in request and response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 1)]
		public ComplexModelResponse responseModelRef1;

		// pure input value, not present in response
		// ComplexObject data1

		// ref (in and out) param, present both in request and response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 2)]
		public ComplexModelResponse responseModelRef2;

		// pure input value, not present in response
		// ComplexObject data2

		// pure output param, present only in response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 3)]
		public ComplexModelResponse responseModelOut1;

		// pure output param, present only in response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 4)]
		public ComplexModelResponse responseModelOut2;

		// pure input value
		// not present in response
		// ComplexObject data2
	}
}
