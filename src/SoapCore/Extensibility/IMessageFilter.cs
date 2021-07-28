using System;
using System.ServiceModel.Channels;

namespace SoapCore.Extensibility
{
	[Obsolete]
	public interface IMessageFilter
	{
		void OnRequestExecuting(Message message);
		void OnResponseExecuting(Message message);
	}
}
