using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Http;
using SoapCore.Extensibility;
using SoapCore.ServiceModel;

namespace SoapCore.Tests.MessageInspectors.MessageInspector3
{
	public class MessageInspector3Mock : IMessageInspector3
	{
		public static bool AfterReceivedRequestCalled { get; private set; }
		public static bool BeforeSendReplyCalled { get; private set; }
		public static Message LastReceivedMessage { get; private set; }

		public static void Reset()
		{
			LastReceivedMessage = null;
			AfterReceivedRequestCalled = false;
			BeforeSendReplyCalled = false;
		}

		public object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription, HttpContext httpContext)
		{
			LastReceivedMessage = message;
			AfterReceivedRequestCalled = true;

			// validate message
			ValidateMessage(ref message);

			return null;
		}

		public void BeforeSendReply(ref Message reply, ServiceDescription serviceDescription, object correlationState)
		{
			BeforeSendReplyCalled = true;
		}

		private void ValidateMessage(ref Message message)
		{
			throw new FaultException(new FaultReason("Message is invalid."), new FaultCode("Sender"), null);
		}
	}
}
