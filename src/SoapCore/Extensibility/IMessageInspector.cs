using System;
using System.ServiceModel.Channels;

namespace SoapCore.Extensibility
{
	[Obsolete]
	public interface IMessageInspector
	{
		object AfterReceiveRequest(ref Message message);
		void BeforeSendReply(ref Message reply, object correlationState);
	}
}
