using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace SoapCore.Tests.ServiceOperationTuner
{
	public class ClientMessageInspector : IClientMessageInspector
	{
		private readonly string _customPingValue;

		public ClientMessageInspector(string customPingValue)
		{
			_customPingValue = customPingValue;
		}

		public void AfterReceiveReply(ref Message reply, object correlationState)
		{
		}

		public object BeforeSendRequest(ref Message request, IClientChannel channel)
		{
			HttpRequestMessageProperty httpRequestMessage;
			object httpRequestMessageObject;

			if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))
			{
				httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
			}
			else
			{
				httpRequestMessage = new HttpRequestMessageProperty();
				request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
			}

			httpRequestMessage.Headers["ping_value"] = _customPingValue;

			return Guid.NewGuid();
		}
	}
}
