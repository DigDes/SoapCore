using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[ServiceContract]
	public interface ISampleService
	{
		[OperationContract]
		string Ping(string s);

		// if enabled, can handle XmlSerializer, but not DataContractSerializer
		// [XmlSerializerFormat]
		[OperationContract]
		ComplexModel2 PingComplexModel1(ComplexModel1 inputModel);

		// if enabled, can handle XmlSerializer, but not DataContractSerializer
		// [XmlSerializerFormat]
		[OperationContract]
		ComplexModel1 PingComplexModel2(ComplexModel2 inputModel);

		// if enabled, can handle XmlSerializer, but not DataContractSerializer
		// [XmlSerializerFormat]
		[OperationContract]
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
		// this is required to deal with enums, fine with DC and Xml
		// seems if any more complex data, and DataContractSerializer will be broken
		[OperationContract]
		[XmlSerializerFormat]
		bool EnumMethod(out SampleEnum e);

		[OperationContract]
		void VoidMethod(out string s);

		[OperationContract]
		Task<int> AsyncMethod();

		[OperationContract]
		int? NullableMethod(bool? arg);

		// if enabled, can handle XmlSerializer, but not DataContractSerializer
		// [XmlSerializerFormat]
		[OperationContract]
		ComplexModel1 EmptyParamsMethod();

		[OperationContract]
		Stream GetStream();
	}
}
