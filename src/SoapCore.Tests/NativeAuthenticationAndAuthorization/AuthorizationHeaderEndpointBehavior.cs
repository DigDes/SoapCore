using System;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace SoapCore.Tests.NativeAuthenticationAndAuthorization
{
	internal class AuthorizationHeaderEndpointBehavior : IEndpointBehavior
	{
		private readonly string _authorizationHeader;
		public AuthorizationHeaderEndpointBehavior(string authorizationHeader)
		{
			_authorizationHeader = authorizationHeader;
		}

		public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
		{
			bindingParameters.Add(new Func<HttpClientHandler, HttpMessageHandler>(x =>
			{
				return new AuthorizationHeaderMessageHandler(x, _authorizationHeader);
			}));
		}

		public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
		{
		}

		public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
		{
		}

		public void Validate(ServiceEndpoint endpoint)
		{
		}
	}
}
