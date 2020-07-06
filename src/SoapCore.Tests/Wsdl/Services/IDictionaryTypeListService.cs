using System;
using System.Collections.Generic;
using System.ServiceModel;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IDictionaryTypeListService
	{
		[OperationContract]
		List<ComplexModelInput> Test();

		[OperationContract]
		Dictionary<string, string> DictionaryTest(Dictionary<string, string> thing);
	}

	public class DictionaryTypeListService : IDictionaryTypeListService
	{
		public Dictionary<string, string> DictionaryTest(Dictionary<string, string> thing)
		{
			throw new NotImplementedException();
		}

		public List<ComplexModelInput> Test() => throw new NotImplementedException();
	}
}
