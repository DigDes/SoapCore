using System.ServiceModel;

namespace SoapCore.Tests
{
	[ServiceContract(Namespace = "http://localhost/MockSoapService")]
	public interface ISoapService
	{
		[OperationContract(Action = "http://localhost/MockSoapService/GetMock")]
		string Get();
	}
}
