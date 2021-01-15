using System.ServiceModel;
using System.Threading.Tasks;
using SoapCore.Tests.OperationDescription.Model;

namespace SoapCore.Tests.MessageContract.Models
{
	[ServiceContract(Namespace = "http://tempuri.org")]
	public interface IServiceWithMessageContract
	{
		[OperationContract]
		[XmlSerializerFormat(SupportFaults = true)]
		string EmptyRequest(Model.MessageContractRequestEmpty req);

		[OperationContract]
		[XmlSerializerFormat(SupportFaults = true)]
		Model.MessageContractResponse DoRequest(Model.MessageContractRequest req);

		[OperationContract]
		[XmlSerializerFormat(SupportFaults = true)]
		Model.MessageContractResponseNotWrapped DoRequest2(Model.MessageContractRequestNotWrapped req);
	}
}
