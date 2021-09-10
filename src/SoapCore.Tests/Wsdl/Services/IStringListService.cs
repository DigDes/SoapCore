using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IStringListService
	{
		[OperationContract]
		List<string> Test();

		[OperationContract]
		ArrayOfStringModel TestWithModel();
	}

	public class StringListService : IStringListService
	{
		public List<string> Test() => throw new NotImplementedException();
		public ArrayOfStringModel TestWithModel() => throw new NotImplementedException();
	}
}
