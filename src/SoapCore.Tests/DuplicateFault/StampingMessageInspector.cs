using System.ServiceModel.Channels;
using SoapCore.Extensibility;
using SoapCore.ServiceModel;

namespace SoapCore.Tests.DuplicateFault
{
	public class StampingMessageInspector : IMessageInspector2
	{
		public const string HeaderName = "InspectorStamp";
		public const string HeaderNamespace = "urn:soapcore-tests";
		public const string HeaderValue = "was-here";

		private int _afterReceiveRequestCallCount;
		private int _beforeSendReplyCallCount;

		public int AfterReceiveRequestCallCount => _afterReceiveRequestCallCount;

		public int BeforeSendReplyCallCount => _beforeSendReplyCallCount;

		public object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription)
		{
			_afterReceiveRequestCallCount++;

			return null;
		}

		public void BeforeSendReply(ref Message reply, ServiceDescription serviceDescription, object correlationState)
		{
			_beforeSendReplyCallCount++;

			reply.Headers.Add(MessageHeader.CreateHeader(HeaderName, HeaderNamespace, HeaderValue));
		}
	}
}
