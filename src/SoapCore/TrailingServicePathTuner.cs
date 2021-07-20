using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SoapCore
{
	/// <summary>
	/// This tuner truncates the incoming http request to the last path-part. ie. /DynamicPath/Service.svc becomes /Service.svc
	/// Register this tuner in ConfigureServices: services.TryAddSingleton&lt;TrailingServicePathTuner&gt;();
	/// </summary>
	public class TrailingServicePathTuner
	{
		public virtual void ConvertPath(HttpContext httpContext)
		{
			string trailingPath = httpContext.Request.Path.Value.Substring(httpContext.Request.Path.Value.LastIndexOf('/'));
			httpContext.Request.Path = new PathString(trailingPath);
		}
	}
}
