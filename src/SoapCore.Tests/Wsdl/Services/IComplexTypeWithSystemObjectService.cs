using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IComplexTypeWithSystemObjectService
	{
		[OperationContract]
		ComplexTypeWithSystemObject Test();
	}

	public class ComplexTypeWithSystemObjectService : IComplexTypeWithSystemObjectService
	{
		public ComplexTypeWithSystemObject Test() => throw new NotImplementedException();
	}
}
