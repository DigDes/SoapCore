using System.Net.Http;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace SoapCore.Tests.NativeAuthenticationAndAuthorization
{
	internal class AuthorizationHeaderMessageHandler : DelegatingHandler
	{
		private readonly string _authorizationHeader;
		public AuthorizationHeaderMessageHandler(HttpClientHandler httpClientHandler, string authorizationHeader)
		{
			InnerHandler = httpClientHandler;
			_authorizationHeader = authorizationHeader;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			request.Headers.Add("Authorization", _authorizationHeader);

			return await base.SendAsync(request, cancellationToken);
		}
	}
}
