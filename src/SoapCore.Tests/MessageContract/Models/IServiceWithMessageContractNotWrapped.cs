using System.ServiceModel;
using System.Threading.Tasks;
using SoapCore.Tests.OperationDescription.Model;

namespace SoapCore.Tests.MessageContract.Models
{
	[ServiceContract(Namespace = "http://tempuri.org")]
	public interface IServiceWithMessageContractNotWrapped
	{
		[OperationContract]
		[XmlSerializerFormat(SupportFaults = true)]
		Model.MessageContractResponseNotWrapped PullData(Model.MessageContractRequestNotWrapped req);
	}
}
