using System.ServiceModel;
using System.Threading.Tasks;

namespace Models
{
	[ServiceContract(Namespace = ServiceNamespace.Value, ConfigurationName = "SampleService.SampleServiceSoap")]
	public interface ISampleService
	{
		[OperationContract(Action = ServiceNamespace.Value + nameof(Ping), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		string Ping(string s);

		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModel), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		ComplexModelResponse PingComplexModel(ComplexModelInput inputModel);

		// new style call with multiple out/ref/value params
		//   instead of packing them into single request/response class
		// produced/consumed xml response is compatible with legacy wcf/ws
		// not sure that this operation contract can be consumed in legacy wcf/ws sources, you may check
		// attention! out params should be last to work correctly! (no idea why)
		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModelOutAndRef), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		bool PingComplexModelOutAndRef(
			ComplexModelInput inputModel,
			ref ComplexModelResponse responseModelRef1,
			ComplexObject data1,
			ref ComplexModelResponse responseModelRef2,
			ComplexObject data2,
			out ComplexModelResponse responseModelOut1,
			out ComplexModelResponse responseModelOut2);

		// old style call, with all in/out params packed into single request/response params
		// both styles are completely compatible, if we have same method name
		//   and req/resp classes as {MethodName}{Request|Response}
		// produced/consumed xml response is compatible with legacy wcf/ws
		// this operation contract is exactly as in legacy wcf/ws sources and can be consumed there
		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModelOldStyle), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		PingComplexModelOldStyleResponse PingComplexModelOldStyle(
			PingComplexModelOldStyleRequest request);

		[OperationContract(Action = ServiceNamespace.Value + nameof(EnumMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		bool EnumMethod(out SampleEnum e);

		[OperationContract(Action = ServiceNamespace.Value + nameof(VoidMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		void VoidMethod(out string s);

		[OperationContract(Action = ServiceNamespace.Value + nameof(AsyncMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		Task<int> AsyncMethod();

		[OperationContract(Action = ServiceNamespace.Value + nameof(NullableMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		int? NullableMethod(bool? arg);
	}
}
