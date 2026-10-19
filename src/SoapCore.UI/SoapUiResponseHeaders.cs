using Microsoft.AspNetCore.Http;

namespace SoapCore.UI
{
	/// <summary>
	/// The security headers written on every response produced by the explorer routes.
	/// </summary>
	internal static class SoapUiResponseHeaders
	{
		public const string ContentSecurityPolicy =
			"default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'; object-src 'none'";

		public static void Apply(HttpResponse response, bool noStore)
		{
			var headers = response.Headers;
			headers["Content-Security-Policy"] = ContentSecurityPolicy;
			headers["X-Content-Type-Options"] = "nosniff";
			headers["Referrer-Policy"] = "no-referrer";
			headers["X-Frame-Options"] = "DENY";

			// The API answer is never stored. HTML and assets are revalidated through their ETag.
			headers["Cache-Control"] = noStore ? "no-cache, no-store" : "no-cache";
		}
	}
}
