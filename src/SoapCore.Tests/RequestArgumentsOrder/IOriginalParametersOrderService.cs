using System.ServiceModel;

namespace SoapCore.Tests.RequestArgumentsOrder
{
	[ServiceContract(Namespace = ServiceNamespace.Value)]
	public interface IOriginalParametersOrderService
	{
		[OperationContract]
		string TwoStringParameters(string first, string second);
	}
}
