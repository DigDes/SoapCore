using System.ServiceModel.Channels;

namespace SoapCore.Tests.MessageInspector
{
	public class MessageInspectorMock : IMessageInspector
	{
		public static bool AfterReceivedRequestCalled { get; private set; }
		public static bool BeforeSendReplyCalled { get; private set; }
		public static Message LastReceivedMessage { get; private set; }

		public void AfterReceiveRequest(Message message)
		{
			if (message == null)
				throw new System.ArgumentNullException(nameof(message));

			LastReceivedMessage = message;
			AfterReceivedRequestCalled = true;
		}

		public void BeforeSendReply(Message reply)
		{
			if (reply == null)
				throw new System.ArgumentNullException(nameof(reply));

			BeforeSendReplyCalled = true;
		}

		public static void Reset()
		{
			LastReceivedMessage = null;
			AfterReceivedRequestCalled = false;
			BeforeSendReplyCalled = false;
		}
	}
}
