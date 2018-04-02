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

		[OperationContract(Name = "OperationNameTest")]
		bool OperationName();

		[OperationContract]
		string Overload(string s);

		[OperationContract(Name = "OverloadDouble")]
		string Overload(double d);

		[OperationContract]
		void OutParam(out string message);

		[OperationContract]
		void RefParam(ref string message);

		[OperationContract]
		void ThrowException();

		[OperationContract]
		void ThrowExceptionWithMessage(string message);
	}
}
