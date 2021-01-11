using System.ServiceModel;
using System.Threading.Tasks;
using SoapCore.Tests.OperationDescription.Model;

namespace SoapCore.Tests.MessageContract
{
	[ServiceContract]
	public interface IServiceWithMessageContract
	{
		[OperationContract]
		string EmptyRequest(Model.MessageContractRequestEmpty req);
	}
}
