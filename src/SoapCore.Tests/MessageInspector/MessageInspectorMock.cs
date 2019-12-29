using System.IO;
using System.ServiceModel.Channels;
using System.Xml;
using SoapCore.Extensibility;

namespace SoapCore.Tests.MessageInspector
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
			if (message == null)
			{
				throw new System.ArgumentNullException(nameof(message));
			}

			LastReceivedMessage = message;
			AfterReceivedRequestCalled = true;

			using (var buffer = message.CreateBufferedCopy(int.MaxValue))
			{
				CorrelationStateMessage state;
				using (var stringWriter = new StringWriter())
				{
					using (var xmlTextWriter = new XmlTextWriter(stringWriter))
					{
						buffer.CreateMessage().WriteMessage(xmlTextWriter);
						xmlTextWriter.Flush();
						xmlTextWriter.Close();

						state = new CorrelationStateMessage
						{
							InternalUID = "Foo",
							Message = stringWriter.ToString()
						};
					}
				}

				// Assign an new message because body can be read only once...
				message = buffer.CreateMessage();

				return state;
			}
		}

		public void BeforeSendReply(ref Message reply, object correlationState)
		{
			if (reply == null)
			{
				throw new System.ArgumentNullException(nameof(reply));
			}

			if (correlationState == null)
			{
				throw new System.ArgumentNullException(nameof(correlationState));
			}

			if (correlationState is CorrelationStateMessage state)
			{
				if (state.InternalUID != "Foo")
				{
					throw new System.Exception("InternalUID not correct");
				}
			}

			BeforeSendReplyCalled = true;
		}

		internal class CorrelationStateMessage
		{
			internal string InternalUID { get; set; }
			internal string Message { get; set; }
		}
	}
}
