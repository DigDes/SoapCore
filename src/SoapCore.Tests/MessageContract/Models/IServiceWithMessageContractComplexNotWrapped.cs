using System;
using System.ServiceModel;

namespace SoapCore.Tests.MessageContract.Models
{
	[ServiceContract(Namespace = "http://tempuri.org")]
	public interface IServiceWithMessageContractComplexNotWrapped
	{
		[OperationContract]
		[XmlSerializerFormat(SupportFaults = true)]
		Model.MessageContractResponseNotWrapped PostData(Model.MessageContractRequestComplexNotWrapped req);
	}
}
