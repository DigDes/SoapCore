using Microsoft.AspNetCore.Http;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// Interface for tuning each operation call
	/// </summary>
	public interface IServiceOperationTuner
	{
		/// <summary>
		/// Tune operation call.
		/// Use this method if it is needed to do some extra configs for operation call.
		/// For example if you need to get some data from http header for some of operations.
		/// </summary>
		/// <param name="httpContext">Current http context</param>
		/// <param name="serviceInstance">Service instance</param>
		/// <param name="operation">Operation description</param>
		void Tune(HttpContext httpContext, object serviceInstance, OperationDescription operation);
	}
}
