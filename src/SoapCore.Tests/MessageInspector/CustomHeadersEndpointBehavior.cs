using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace SoapCore.Tests.MessageInspector
{
	[Obsolete]
	public class CustomHeadersEndpointBehavior : IEndpointBehavior
	{
		private Dictionary<string, object> _customHeaders;

		public CustomHeadersEndpointBehavior(Dictionary<string, object> customHeaders)
		{
			_customHeaders = customHeaders;
		}

		public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
		{
			clientRuntime.ClientMessageInspectors.Add(new ClientMessageInspector(_customHeaders));
		}

		public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
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
