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
		// instead of packing them into single request/response class
		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModelOutAndRef), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		bool PingComplexModelOutAndRef(
			ComplexModelInput inputModel,
			ref ComplexModelResponse responseModelRef,
			ComplexObject data1,
			out ComplexModelResponse responseModelOut,
			ComplexObject data2);

		// old style call, with all in/out params packed into single request/response params
		// both styles are completely compatible, if we have same method name
		// and req/resp classes as {MethodName}{Request|Response}
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
