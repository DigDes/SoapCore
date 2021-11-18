using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Http;
using SoapCore.ServiceModel;

namespace SoapCore.Extensibility
{
	public interface IMessageInspector3
	{
		object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription, HttpContext httpContext);
		void BeforeSendReply(ref Message reply, ServiceDescription serviceDescription, object correlationState);
	}
}
