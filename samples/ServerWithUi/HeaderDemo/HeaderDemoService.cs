using System.ServiceModel;

namespace ServerWithUi.HeaderDemo
{
	public sealed class HeaderDemoService : IHeaderDemoService
	{
		private const string TokenHeaderName = "X-Demo-Token";

		// Demo value only, not a secret.
		private const string DemoToken = "demo";

		private readonly IHttpContextAccessor _httpContextAccessor;

		public HeaderDemoService(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public string Echo(string text)
		{
			return text;
		}

		public string EchoWithToken(string text)
		{
			var token = _httpContextAccessor.HttpContext?.Request.Headers[TokenHeaderName].ToString();
			if (!string.Equals(token, DemoToken, StringComparison.Ordinal))
			{
				throw new FaultException(
					new FaultReason("Missing or invalid X-Demo-Token header. Add it under 'Custom request headers' (value: demo)."),
					new FaultCode("Client"),
					string.Empty);
			}

			return "Authorized echo: " + text;
		}
	}
}
