using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SoapCore
{
	/// <summary>
	/// Register this tuner in ConfigureServices: services.AddSoapServiceOperationTuner(new TrailingServicePathTuner));
	/// </summary>
	public class TrailingServicePathTuner : IServiceOperationTuner
	{
		public void Tune(HttpContext httpContext, object serviceInstance, OperationDescription operation)
		{
			string trailingPath = httpContext.Request.Path.Value.Substring(httpContext.Request.Path.Value.LastIndexOf('/'));
			httpContext.Request.Path = new PathString(trailingPath);
		}
	}
}
