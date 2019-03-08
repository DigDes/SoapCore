using System.ServiceModel;
using System.Threading.Tasks;
using SoapCore.Tests.OperationDescription.Model;

namespace SoapCore.Tests.OperationDescription
{
	public interface IServiceWithMessageContract
	{
		Task<CoolResponseClass> GetMyClass();

		CoolResponseClass GetMyOtherClass();

		string GetMyStringClass();

		[FaultContract(typeof(TypedSoapFault))]
		void ThrowTypedFault();
	}
}
