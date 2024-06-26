using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IGenericDataContractService
	{
		[OperationContract]
		GenericDataContractService.MyType<string> TestString();

		[OperationContract]
		GenericDataContractService.MyType<GenericDataContractService.MyArg> TestMyArg();
	}

	public class GenericDataContractService : IGenericDataContractService
	{
		public MyType<string> TestString() => throw new NotImplementedException();

		public MyType<MyArg> TestMyArg() => throw new NotImplementedException();

		[DataContract(Name = "My{0}Type", Namespace = "http://testnamespace.org")]
		public class MyType<T>
		{
			[DataMember]
			public T Value { get; set; }
		}

		public class MyArg
		{
			public string Value { get; set; }
		}
	}
}
