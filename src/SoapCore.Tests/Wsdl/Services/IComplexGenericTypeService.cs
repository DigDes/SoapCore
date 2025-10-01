using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IComplexGenericTypeService
	{
		[OperationContract]
		void Test(ComplexTypeWithGeneric model);
	}

	public class ComplexGenericTypeService : IComplexGenericTypeService
	{
		public void Test(ComplexTypeWithGeneric model) => throw new NotImplementedException();
	}
}
