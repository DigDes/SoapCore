using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore
{
	public interface IOperationInvoker
	{
		Task<object> InvokeAsync(MethodInfo methodInfo, object instance, object[] inputs);
	}
}
