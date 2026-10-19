using System.Runtime.InteropServices;
using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// A contract SoapCore's ServiceDescription cannot describe: an [Out] parameter that is not passed by reference makes it throw.
	// The explorer must contain that failure to this one endpoint.
	[ServiceContract(Namespace = "urn:ui-broken")]
	public interface IUiBrokenService
	{
		[OperationContract]
		string Broken([Out] string text);
	}
}
