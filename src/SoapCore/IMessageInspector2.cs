using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore
{
	public interface IMessageInspector2
	{
		object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription);
		void BeforeSendReply(ref Message reply, ServiceDescription serviceDescription, object correlationState);
	}
}
