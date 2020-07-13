using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface ISystemTypesService
	{
		[OperationContract]
		void Method(Uri value);
	}

	public class SystemTypesService : ISystemTypesService
	{
		public void Method(Uri value)
		{
			throw new NotImplementedException();
		}
	}
}
