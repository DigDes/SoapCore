using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using System.Text;
using SoapCore;
using SoapCore.Extensibility;

namespace ServerWithUi.Secure
{
	/// <summary>
	/// DEMO ONLY. Protects /Secure.svc so that the "Authorize" panel of the explorer has something to talk to.
	/// The credentials below are public: they are printed in the README of the sample. Never reuse this code, or these values, to protect a real service.
	/// <para>
	/// A SOAP request to /Secure.svc is accepted when it carries ANY ONE of these:
	/// </para>
	/// <list type="bullet">
	/// <item><description>the header <c>Authorization: Bearer demo-token</c>;</description></item>
	/// <item><description>HTTP Basic: <c>Authorization: Basic</c> with the user name <c>demo</c> and the password <c>demo-password</c>;</description></item>
	/// <item><description>the header <c>X-Api-Key: demo-key</c>;</description></item>
	/// <item><description>a WS-Security UsernameToken with the user name <c>demo</c> and the password <c>demo-password</c>, as plain text (PasswordText) or as a digest with nonce and created (PasswordDigest).</description></item>
	/// </list>
	/// <para>
	/// The HTTP headers are checked first. Only when none of them matches and the message has a <c>Security</c> header, the UsernameToken is checked,
	/// with <see cref="WsMessageFilter"/>: the same validation that <c>AddSoapWsSecurityFilter</c> gives a real endpoint.
	/// Anything else is answered with a SOAP fault and HTTP 401.
	/// </para>
	/// <para>
	/// It is registered with <c>AddSoapMessageProcessor</c>, so it sees the SOAP requests of every endpoint, and does nothing for any path other than /Secure.svc.
	/// It does not see GET requests: <see cref="IsOperationRoute"/> is used by Program.cs to close the GET and POST routes of SoapCore that skip message processors.
	/// The secrets are compared in constant time. There is no protection against replay, and the WSDL (GET ?wsdl) is public, because the explorer needs it.
	/// </para>
	/// </summary>
	public sealed class SecureDemoMessageProcessor : ISoapMessageProcessor
	{
		/// <summary>
		/// The path of the protected endpoint.
		/// </summary>
		public const string EndpointPath = "/Secure.svc";

		/// <summary>
		/// The header of the API key scheme. It is also what the explorer is told to use (SoapUiOptions.ApiKeyHeaderName).
		/// </summary>
		public const string ApiKeyHeaderName = "X-Api-Key";

		/// <summary>
		/// The key under which the accepted scheme is kept in <c>HttpContext.Items</c>, for <see cref="SecureService"/>.
		/// </summary>
		public const string SchemeItemKey = "ServerWithUi.Secure.Scheme";

		// Demo credentials. Public on purpose.
		private const string BearerToken = "demo-token";
		private const string UserName = "demo";
		private const string Password = "demo-password";
		private const string ApiKey = "demo-key";

		private const string AuthenticationRequiredMessage =
			"Authentication required. Send a bearer token, HTTP Basic credentials, the X-Api-Key header or a WS-Security UsernameToken. The demo credentials are in the README of the sample.";

		private readonly WsMessageFilter _wsSecurity = new WsMessageFilter(UserName, Password);

		/// <summary>
		/// Gets a value indicating whether the path is below /Secure.svc, as in /Secure.svc/Echo. SoapCore handles those paths itself
		/// (GET {path}/{Operation}?p=... when GET is enabled, which the explorer needs for ?wsdl), and a message processor never sees them.
		/// </summary>
		/// <param name="path">The request path (without the PathBase).</param>
		/// <returns><c>true</c> for /Secure.svc/Anything, <c>false</c> for /Secure.svc and /Secure.svc/ itself and for any other path.</returns>
		public static bool IsOperationRoute(PathString path)
		{
			return path.StartsWithSegments(EndpointPath, StringComparison.OrdinalIgnoreCase, out var remaining)
				&& remaining.HasValue
				&& remaining.Value != "/";
		}

		public async Task<Message> ProcessMessage(Message message, HttpContext context, Func<Message, Task<Message>> next)
		{
			if (!context.Request.Path.StartsWithSegments(EndpointPath, StringComparison.OrdinalIgnoreCase))
			{
				return await next(message);
			}

			var scheme = AuthenticateHeaders(context.Request);
			if (scheme == null)
			{
				if (!HasSecurityHeader(message))
				{
					throw new AuthenticationException(AuthenticationRequiredMessage);
				}

				// Throws an AuthenticationException (HTTP 401) when the token is missing, malformed or wrong. Only headers are read: the body is not touched.
				await _wsSecurity.OnRequestExecuting(message);
				scheme = "WsUsernameToken";
			}

			context.Items[SchemeItemKey] = scheme;
			return await next(message);
		}

		private static string AuthenticateHeaders(HttpRequest request)
		{
			if (AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var authorization) && authorization.Parameter != null)
			{
				if (string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) && FixedTimeEquals(authorization.Parameter, BearerToken))
				{
					return "Bearer";
				}

				if (string.Equals(authorization.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) && IsValidBasic(authorization.Parameter))
				{
					return "Basic";
				}
			}

			if (FixedTimeEquals(request.Headers[ApiKeyHeaderName].ToString(), ApiKey))
			{
				return "ApiKey";
			}

			return null;
		}

		private static bool IsValidBasic(string parameter)
		{
			string decoded;
			try
			{
				decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parameter));
			}
			catch (FormatException)
			{
				return false;
			}

			var separator = decoded.IndexOf(':');
			if (separator < 0)
			{
				return false;
			}

			// Both are always compared (& and not &&), so the time does not tell which one was wrong.
			var userMatches = FixedTimeEquals(decoded.Substring(0, separator), UserName);
			var passwordMatches = FixedTimeEquals(decoded.Substring(separator + 1), Password);
			return userMatches & passwordMatches;
		}

		private static bool HasSecurityHeader(Message message)
		{
			for (var i = 0; i < message.Headers.Count; i++)
			{
				if (string.Equals(message.Headers[i].Name, "Security", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		// Hashing both sides first gives them the same length, so even the length of the secret is not leaked.
		private static bool FixedTimeEquals(string actual, string expected)
		{
			var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(actual ?? string.Empty));
			var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
			return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
		}
	}
}
