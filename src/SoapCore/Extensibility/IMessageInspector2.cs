using System.ServiceModel.Channels;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	public interface IMessageInspector2
	{
		object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription);
		void BeforeSendReply(ref Message reply, ServiceDescription serviceDescription, object correlationState);
	}
}
