using System.ServiceModel;
using System.Threading.Tasks;

namespace SoapCore.Tests
{
	[ServiceContract]
	public interface ITestService
	{
		[OperationContract]
		string Ping(string s);

		[OperationContract]
		string EmptyArgs();

		[OperationContract]
		string SingleInteger(int i);

		[OperationContract]
		Task<string> AsyncMethod();

		[OperationContract]
		bool IsNull(double? d);
	}
}
