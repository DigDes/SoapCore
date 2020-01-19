using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IDataContractNameService
	{
		[OperationContract]
		DataContractNameService.ChildClass Test();
	}

	public class DataContractNameService : IDataContractNameService
	{
		public ChildClass Test() => throw new NotImplementedException();

		[DataContract(Name = "BaseRenamed")]
		public class BaseClass
		{
			[DataMember]
			public string BaseProp { get; set; }
		}

		[DataContract(Name = "ChildRenamed")]
		public class ChildClass : BaseClass
		{
			[DataMember]
			public string ChildProp { get; set; }
		}
	}
}
