using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface ICollectionDataContractService
	{
		[OperationContract]
		CollectionDataContractService.MyList<string> ListStrings();

		[OperationContract]
		CollectionDataContractService.MyList<CollectionDataContractService.MyType> ListMyTypes();
	}

	public class CollectionDataContractService : ICollectionDataContractService
	{
		public MyList<string> ListStrings() => throw new NotImplementedException();
		public MyList<MyType> ListMyTypes() => throw new NotImplementedException();

		[CollectionDataContract(Namespace = "http://testnamespace.org", Name = "My{0}List", ItemName = "MyItem")]
		public class MyList<T> : List<T>
		{
		}

		public class MyType
		{
			public string MyProperty { get; set; }
		}
	}
}
