using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IComplexTypeAndOutParameterService
	{
		[OperationContract]
		ComplexType Method(out string message);
	}

	public class ComplexTypeAndOutParameterService : IComplexTypeAndOutParameterService
	{
		public ComplexType Method(out string message)
		{
			throw new NotImplementedException();
		}
	}
}
