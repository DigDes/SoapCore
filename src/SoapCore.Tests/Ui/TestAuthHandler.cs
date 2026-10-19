using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SoapCore.Tests.Ui
{
	// Minimal authentication scheme for the UI tests: a request is authenticated when it carries the X-Test-User header.
	public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
	{
		public const string SchemeName = "Test";
		public const string UserHeader = "X-Test-User";

		public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
			: base(options, logger, encoder)
		{
		}

		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			if (!Request.Headers.TryGetValue(UserHeader, out var values) || string.IsNullOrEmpty(values.ToString()))
			{
				return Task.FromResult(AuthenticateResult.NoResult());
			}

			var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, values.ToString()) }, SchemeName);
			var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

			return Task.FromResult(AuthenticateResult.Success(ticket));
		}
	}
}
