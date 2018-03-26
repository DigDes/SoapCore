using System.ServiceModel.Channels;

namespace SoapCore
{
	public interface IMessageInspector
    {
		void AfterReceiveRequest(Message message);
		void BeforeSendReply(Message reply);
	}
}
