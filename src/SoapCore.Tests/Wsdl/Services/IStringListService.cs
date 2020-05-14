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
	}

	public class StringListService : IStringListService
	{
		public List<string> Test() => throw new NotImplementedException();
	}
}
