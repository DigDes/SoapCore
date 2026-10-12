using System.ComponentModel;
using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// A contract with descriptions that cannot be read, next to one that can: the failure must stay where it happens.
	[ServiceContract(Namespace = "urn:ui-throwing-description")]
	public interface IUiThrowingDescriptionService
	{
		[UiThrowingDescription]
		[OperationContract]
		string Broken([UiThrowingDescription] string input, string other);

		[Description("Still fine.")]
		[OperationContract]
		string Fine([Description("Fine parameter.")] string input);
	}
}
