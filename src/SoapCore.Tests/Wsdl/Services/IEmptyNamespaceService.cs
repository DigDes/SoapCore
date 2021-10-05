using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract(Namespace = "")]
	public interface IEmptyNamespaceService
	{
		[OperationContract]
		void TestMethod();
	}

	public class EmptyNamespaceService : IEmptyNamespaceService
	{
		public void TestMethod()
		{
			// Do nothing
		}
	}
}