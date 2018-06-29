using System.ServiceModel.Channels;

namespace SoapCore
{
	public interface IMessageFilter
	{
		void OnRequestExecuting(Message message);
		void OnResponseExecuting(Message message);
	}
}
