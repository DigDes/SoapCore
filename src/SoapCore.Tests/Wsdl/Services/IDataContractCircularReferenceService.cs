using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IDataContractCircularReferenceService
	{
		[OperationContract]
		CircularReferenceFirstObject GetFirstObject();
	}

	public class DataContractCircularReferenceService : IDataContractCircularReferenceService
	{
		public CircularReferenceFirstObject GetFirstObject()
		{
			throw new NotImplementedException();
		}
	}
}
