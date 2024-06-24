using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IServiceWithFaultContracts
	{
		[OperationContract]
		[FaultContract(typeof(OperationFault))]
		EnumWithCustomNames? GetEnum(string text);

		[OperationContract]
		[FaultContract(typeof(FailedOperation))]
		ComplexType LoadComplexType(out string message);
	}

	public class ServiceWithFaultContracts : IServiceWithFaultContracts
	{
		public EnumWithCustomNames? GetEnum(string text)
		{
			throw new NotImplementedException();
		}

		public ComplexType LoadComplexType(out string message)
		{
			throw new NotImplementedException();
		}
	}
}
