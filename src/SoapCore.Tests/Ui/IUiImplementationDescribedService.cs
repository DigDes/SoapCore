using System.ComponentModel;
using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// A contract with little documentation of its own: the implementation (UiImplementationDescribedService) documents the rest.
	[ServiceContract(Namespace = "urn:ui-implementation-described")]
	public interface IUiImplementationDescribedService
	{
		[OperationContract]
		string Work(string input);

		[Description("From the contract.")]
		[OperationContract]
		string Both([Description("Contract parameter.")] string input);
	}
}
