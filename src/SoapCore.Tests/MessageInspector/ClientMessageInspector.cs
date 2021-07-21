using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace SoapCore.Tests.MessageInspector
{
	[Obsolete]
	public class ClientMessageInspector : IClientMessageInspector
	{
		private readonly Dictionary<string, object> _customHeaders;

		public ClientMessageInspector(Dictionary<string, object> customHeaders)
		{
			_customHeaders = customHeaders;
		}

		public void AfterReceiveReply(ref Message reply, object correlationState)
		{
		}

		public object BeforeSendRequest(ref Message request, IClientChannel channel)
		{
			foreach (var kvp in _customHeaders)
			{
				var header = MessageHeader.CreateHeader(kvp.Key, "SoapCore", kvp.Value);
				request.Headers.Add(header);
			}

			return Guid.NewGuid();
		}
	}
}
