using System.ServiceModel.Channels;

namespace SoapCore.Extensibility
{
	public interface IMessageInspector
	{
		object AfterReceiveRequest(ref Message message);
		void BeforeSendReply(ref Message reply, object correlationState);
	}
}
