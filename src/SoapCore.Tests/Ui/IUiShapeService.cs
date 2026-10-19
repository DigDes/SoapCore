using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	[ServiceContract(Namespace = "urn:ui-shape")]
	public interface IUiShapeService
	{
		[OperationContract]
		UiShapeModels.Reply Get(UiShapeModels.Request request);
	}
}
