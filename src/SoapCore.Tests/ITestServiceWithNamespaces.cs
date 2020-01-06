using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SoapCore.Tests.Model;

namespace SoapCore.Tests
{
	[ServiceContract(Namespace = "ServiceNamespace")]
	public interface ITestServiceWithNamespaces
	{
		[OperationContract(Action = "OperationContract", ReplyAction = "OperationContractResponse")]
		ComplexModelWithNamespacesOutput TestOperation(ComplexModelWithNamespacesInput input);
	}
}
