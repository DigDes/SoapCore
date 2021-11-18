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
		public static Message LastReceivedMessage { get; private set; }

		public static void Reset()
		{
			LastReceivedMessage = null;
			AfterReceivedRequestCalled = false;
		}

		public object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription, HttpContext httpContext)
		{
			LastReceivedMessage = message;
			AfterReceivedRequestCalled = true;

			return ValidateMessage(ref message);
		}

		private string ValidateMessage(ref Message message)
		{
			return "Failed";
		}
	}
}
