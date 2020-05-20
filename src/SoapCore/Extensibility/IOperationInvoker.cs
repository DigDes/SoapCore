using System.Reflection;
using System.Threading.Tasks;

namespace SoapCore.Extensibility
{
	public interface IOperationInvoker
	{
		Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] inputs);
	}
}
