using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore
{
	public class DefaultOperationInvoker : IOperationInvoker
	{
		public async Task<object> InvokeAsync(MethodInfo methodInfo, object serviceInstance, object[] arguments)
		{
			// Invoke Operation method
			var responseObject = methodInfo.Invoke(serviceInstance, arguments);
			if (methodInfo.ReturnType.IsConstructedGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				var responseTask = (Task)responseObject;
				await responseTask;
				responseObject = responseTask.GetType().GetProperty("Result").GetValue(responseTask);
			}
			else if (responseObject is Task responseTask)
			{
				await responseTask;

				//VoidTaskResult
				responseObject = null;
			}

			return responseObject;
		}
	}
}
