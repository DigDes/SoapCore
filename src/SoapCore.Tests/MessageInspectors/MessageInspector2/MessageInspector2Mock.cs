using System.ServiceModel;
using System.ServiceModel.Channels;
using SoapCore.Extensibility;
using SoapCore.ServiceModel;

namespace SoapCore.Tests.MessageInspectors.MessageInspector2
{
	public class MessageInspector2Mock : IMessageInspector2
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

		public object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription)
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
