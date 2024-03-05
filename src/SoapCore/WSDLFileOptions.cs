using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace SoapCore
{
	public class WsdlFileOptions
	{
		public virtual Dictionary<string, WebServiceWSDLMapping> WebServiceWSDLMapping { get; set; } = new Dictionary<string, WebServiceWSDLMapping>();
		public string UrlOverride { get; set; }
		public string SchemeOverride { get; set; }
		public string VirtualPath { get; set; }
		public string AppPath { get; set; }

		public virtual string GetServerUrl(HttpContext httpContext)
		{
			if (!string.IsNullOrEmpty(UrlOverride))
			{
				return UrlOverride;
			}

			string scheme = string.IsNullOrEmpty(SchemeOverride) ? httpContext.Request.Scheme : SchemeOverride;
			string host = httpContext.Request.Host.ToString();
			var forwardedHost = httpContext.Request.Headers["X-Forwarded-Host"];
			if (forwardedHost.Count != 0)
			{
				host = forwardedHost[0];
			}

			return scheme + "://" + host + "/";
		}
	}

	public class WsdlFileOptionsCaseInsensitive : WsdlFileOptions
	{
		public override Dictionary<string, WebServiceWSDLMapping> WebServiceWSDLMapping { get; set; } = new Dictionary<string, WebServiceWSDLMapping>(StringComparer.OrdinalIgnoreCase);
	}
}
