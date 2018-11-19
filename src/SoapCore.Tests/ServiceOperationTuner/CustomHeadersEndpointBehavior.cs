using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace SoapCore.Tests.ServiceOperationTuner
{
	public class CustomHeadersEndpointBehavior : IEndpointBehavior
	{
		private string _customPingValue;

		public CustomHeadersEndpointBehavior(string customPingValue)
		{
			_customPingValue = customPingValue;
		}

		public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
		{
			clientRuntime.ClientMessageInspectors.Add(new ClientMessageInspector(_customPingValue));
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
