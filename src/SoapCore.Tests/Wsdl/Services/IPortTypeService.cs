using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract(Namespace = "http://tempuri.org/IPortTypeService")]
	public interface IPortTypeService : IPortTypeServiceBase
	{
		[OperationContract]
		void Test();
	}

	[ServiceContract(Namespace = "http://tempuri.org/IPortTypeServiceBase")]
	public interface IPortTypeServiceBase
	{
		[OperationContract]
		void TestBase();
	}

	public abstract class PortTypeServiceBase : IPortTypeServiceBase
	{
		public void TestBase()
		{
			throw new System.NotImplementedException();
		}

		public class PortTypeService : PortTypeServiceBase, IPortTypeService
		{
			public void Test()
			{
				// TODO:
			}
		}
	}
}
