using System.ServiceModel;
using System.Runtime.Serialization;

namespace Models
{
	[MessageContract(WrapperName = nameof(ISampleService.PingComplexModelOldStyle), WrapperNamespace = ServiceNamespace.Value, IsWrapped = true)]
	public class PingComplexModelOldStyleRequest
	{
		// pure input value, similar to non-ref param in new style
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 0)]
		public ComplexModelInput inputModel;

		// ref (in and out) param, present both in request and response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 1)]
		public ComplexModelResponse responseModelRef1;

		// pure input value
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 2)]
		public ComplexObject data1;

		// ref (in and out) param, present both in request and response
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 3)]
		public ComplexModelResponse responseModelRef2;

		// pure input value
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 4)]
		public ComplexObject data2;

		// pure output param, not present in request
		// out ComplexModelResponse responseModelOut1
		// out ComplexModelResponse responseModelOut2
	}
}
