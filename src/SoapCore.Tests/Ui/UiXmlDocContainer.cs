using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// A service contract that is a nested type: its id in the XML documentation is T:SoapCore.Tests.Ui.UiXmlDocContainer.INestedService.
	public static class UiXmlDocContainer
	{
		[ServiceContract(Namespace = "urn:ui-xml-nested")]
		public interface INestedService
		{
			[OperationContract]
			string Ping(string text);
		}
	}
}
