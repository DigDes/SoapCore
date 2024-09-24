using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface ISystemImportService
	{
		[OperationContract]
		ComplexType GetValue();
	}

	public class SystemImportService : ISystemImportService
	{
		public ComplexType GetValue()
		{
			throw new NotImplementedException();
		}
	}
}
