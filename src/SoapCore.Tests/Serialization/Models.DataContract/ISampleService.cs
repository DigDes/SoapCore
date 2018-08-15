using System.ServiceModel;
using System.Threading.Tasks;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[ServiceContract]
	public interface ISampleService
	{
		[OperationContract]
		string Ping(string s);

		[OperationContract]
		// if enabled, can handle XmlSerializer, but not DataContractSerializer
		// [XmlSerializerFormat]
		ComplexModel1 PingComplexModel(ComplexModel2 inputModel);

		[OperationContract]
		// if enabled, can handle XmlSerializer, but not DataContractSerializer
		// [XmlSerializerFormat]
		bool PingComplexModelOutAndRef(
			ComplexModel1 inputModel,
			ref ComplexModel2 responseModelRef1,
			ComplexObject data1,
			ref ComplexModel1 responseModelRef2,
			ComplexObject data2,
			out ComplexModel2 responseModelOut1,
			out ComplexModel1 responseModelOut2);

		// this seems interesting and useful case, but definitely incompatible with DC now
		//PingComplexModelOldStyleResponse PingComplexModelOldStyle(
		//	PingComplexModelOldStyleRequest request);

		[OperationContract]
		// this is required to deal with enums, fine with DC and Xml
		// seems if any more complex data, and DataContractSerializer will be broken
		[XmlSerializerFormat]
		bool EnumMethod(out SampleEnum e);

		[OperationContract]
		void VoidMethod(out string s);

		[OperationContract]
		Task<int> AsyncMethod();

		[OperationContract]
		int? NullableMethod(bool? arg);

		[OperationContract]
		// if enabled, can handle XmlSerializer, but not DataContractSerializer
		// [XmlSerializerFormat]
		ComplexModel1 EmptyParamsMethod();
	}
}
