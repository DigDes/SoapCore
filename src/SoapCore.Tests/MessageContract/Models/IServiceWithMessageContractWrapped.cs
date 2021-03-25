using System.ServiceModel;
using System.Threading.Tasks;
using SoapCore.Tests.OperationDescription.Model;

namespace SoapCore.Tests.MessageContract.Models
{
	[ServiceContract(Namespace = "http://tempuri.org")]
	public interface IServiceWithMessageContractWrapped
	{
		[OperationContract]
		[XmlSerializerFormat(SupportFaults = true)]
		Model.MessageContractResponse PullData(Model.MessageContractRequest req);
	}
}
