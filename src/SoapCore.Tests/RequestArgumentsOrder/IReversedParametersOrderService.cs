using System.ServiceModel;

namespace SoapCore.Tests.RequestArgumentsOrder
{
	[ServiceContract(Namespace = ServiceNamespace.Value)]
	public interface IReversedParametersOrderService
	{
		[OperationContract]
		string TwoStringParameters(string second, string first);
	}
}
