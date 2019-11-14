using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[ServiceContract(Namespace = ServiceNamespace.Value, ConfigurationName = "SampleService.SampleServiceSoap")]
	public interface ISampleService
	{
		[OperationContract(Action = ServiceNamespace.Value + nameof(Ping), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		string Ping(string s);

		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModel1), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		ComplexModel2 PingComplexModel1(ComplexModel1 inputModel);

		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModel2), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		ComplexModel1 PingComplexModel2(ComplexModel2 inputModel);

		// new style call with multiple out/ref/value params
		//   instead of packing them into single request/response class
		// produced/consumed xml response is compatible with legacy wcf/ws
		// not sure that this operation contract can be consumed in legacy wcf/ws sources, you may check
		// attention! out params should be last to work correctly! (no idea why)
		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModelOutAndRef), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		bool PingComplexModelOutAndRef(
			ComplexModel1 inputModel,
			ref ComplexModel2 responseModelRef1,
			ComplexObject data1,
			ref ComplexModel1 responseModelRef2,
			ComplexObject data2,
			out ComplexModel2 responseModelOut1,
			out ComplexModel1 responseModelOut2);

		// old style call, when all in/out params are packed into single request/response params
		// both styles are completely compatible, if we have same method name
		//   and req/resp classes as {MethodName}{Request|Response}
		// produced/consumed xml response is compatible with legacy wcf/ws
		// this operation contract is exactly as in legacy wcf/ws sources and can be consumed there
		// to avoid extra envelope element, mark request/reponse type as [MessageContract] and do not use ref/out params
		//   otherwise compatibility will be broken
		//   (effective only for XmlSerializer)
		[OperationContract(Action = ServiceNamespace.Value + nameof(PingComplexModelOldStyle), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		PingComplexModelOldStyleResponse PingComplexModelOldStyle(
			PingComplexModelOldStyleRequest request);

		[OperationContract(Action = ServiceNamespace.Value + nameof(NotWrappedPropertyComplexInputRequestMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		NotWrappedPropertyComplexInputResponse NotWrappedPropertyComplexInputRequestMethod(
			NotWrappedPropertyComplexInputRequest request);

		[OperationContract(Action = ServiceNamespace.Value + nameof(NotWrappedFieldComplexInputRequestMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		NotWrappedFieldComplexInputResponse NotWrappedFieldComplexInputRequestMethod(
			NotWrappedFieldComplexInputRequest request);

		[OperationContract(Action = ServiceNamespace.Value + nameof(NotWrappedFieldDoubleComplexInputRequestMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		NotWrappedFieldComplexInputResponse NotWrappedFieldDoubleComplexInputRequestMethod(
			NotWrappedFieldDoubleComplexInputRequest request);

		// Ideally this would be void however the WCF client requires that if you have a MessageContract
		// response you *must* have a MessageContract input
		[OperationContract(Action = ServiceNamespace.Value + nameof(TestUnwrappedMultipleMessageBodyMember), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		UnwrappedMultipleMessageBodyMemberResponse TestUnwrappedMultipleMessageBodyMember(BasicMessageContractPayload x);

		// Ideally this would be void however WCF client requires that if you have a MessageContract
		// response you *must* have a MessageContract input
		[OperationContract(Action = ServiceNamespace.Value + nameof(TestUnwrappedStringMessageBodyMember), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		UnwrappedStringMessageBodyMemberResponse TestUnwrappedStringMessageBodyMember(BasicMessageContractPayload x);

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

		[OperationContract(Action = ServiceNamespace.Value + nameof(EmptyParamsMethod), ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		ComplexModel1 EmptyParamsMethod();

		[OperationContract]
		[XmlSerializerFormat]
		string[] PingStringArray(string[] array);

		[OperationContract]
		[XmlSerializerFormat]
		ComplexModel1[] PingComplexModelArray(ComplexModel1[] models, ComplexModel2[] models2);

		[OperationContract]
		[XmlSerializerFormat]
		string[] PingStringArrayWithXmlArray([XmlElement("arrayItem")]string[] array);

		[OperationContract]
		[XmlSerializerFormat]
		ComplexModel1[] PingComplexModelArrayWithXmlArray([XmlArrayItem("arr1")]ComplexModel1[] models, [XmlElement("arr2")]ComplexModel2[] models2);

		[OperationContract]
		[XmlSerializerFormat]
		int[] PingIntArray(int[] array);

		[OperationContract]
		[XmlSerializerFormat]
		DataContractWithStream PingStream(DataContractWithStream model);

		[OperationContract]
		[XmlSerializerFormat]
		Stream GetStream();
	}
}
