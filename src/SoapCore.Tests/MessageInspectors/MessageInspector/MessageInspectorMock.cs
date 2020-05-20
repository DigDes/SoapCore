using System.ServiceModel;
using System.ServiceModel.Channels;
using SoapCore.Extensibility;

namespace SoapCore.Tests.MessageInspectors.MessageInspector
{
	public class MessageInspectorMock : IMessageInspector
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

		public object AfterReceiveRequest(ref Message message)
		{
			LastReceivedMessage = message;
			AfterReceivedRequestCalled = true;

			// Validate Message
			ValidateMessage(ref message);

			return null;
		}

		public void BeforeSendReply(ref Message reply, object correlationState)
		{
			BeforeSendReplyCalled = true;
		}

		private void ValidateMessage(ref Message message)
		{
			throw new FaultException(new FaultReason("Message is invalid."), new FaultCode("Sender"), null);
		}
	}
}
