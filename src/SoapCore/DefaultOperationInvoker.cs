using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AspectCore.Extensions.Reflection;

namespace SoapCore
{
	public class DefaultOperationInvoker : IOperationInvoker
	{
		private static readonly List<Type> TaskTypes = new List<Type> { typeof(Task<>), typeof(ValueTask<>) };
		private static readonly ConcurrentDictionary<Type, MethodReflector> TaskResultCache = new ConcurrentDictionary<Type, MethodReflector>();
		private static readonly ConcurrentDictionary<Type, bool> IsTaskResult = new ConcurrentDictionary<Type, bool>();

		private static readonly MethodInfo[] AsMethods = new[]
        {
			new Func<Task<object>, Task<object>>(As).Method.GetGenericMethodDefinition(),
			new Func<ValueTask<object>, Task<object>>(As).Method.GetGenericMethodDefinition(),
		};

		public Task<object> InvokeAsync(MethodInfo methodInfo, object serviceInstance, object[] arguments)
		{
			if (!IsTaskResult.GetOrAdd(methodInfo.ReturnType, type =>
				type.IsConstructedGenericType && TaskTypes.Contains(type.GetGenericTypeDefinition())))
			{
				// Invoke Operation method
				var responseObject = methodInfo.Invoke(serviceInstance, arguments);

				return responseObject is Task task ? As(task) : As(responseObject);
			}

			return (Task<object>)TaskResultCache.GetOrAdd(methodInfo.ReturnType, type =>
				   AsMethods[type.GetGenericTypeDefinition() == typeof(Task<>) ? 0 : 1]
					   .MakeGenericMethod(type.GenericTypeArguments[0]).GetReflector())
				.Invoke(null, methodInfo.Invoke(serviceInstance, arguments));

			//return As((dynamic)methodInfo.Invoke(serviceInstance, arguments));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Task<object> As(object result) => Task.FromResult(result);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async Task<object> As(Task task)
		{
			await task;

			return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async Task<object> As<T>(Task<T> task) => await task;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async Task<object> As<T>(ValueTask<T> task) => await task;
	}
}
