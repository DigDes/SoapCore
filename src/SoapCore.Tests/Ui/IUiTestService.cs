using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	[ServiceContract(Namespace = "urn:ui-tests")]
	public interface IUiTestService
	{
		[OperationContract]
		string Echo(string text);

		[OperationContract(IsOneWay = true)]
		void Fire(string text);

		// The operation name is taken from the segment after the last '/' of the action: "CustomAction".
		[OperationContract(Action = "urn:ui-tests/CustomAction")]
		string Custom();
	}
}
