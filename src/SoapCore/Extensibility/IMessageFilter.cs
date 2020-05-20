using System.ServiceModel.Channels;

namespace SoapCore.Extensibility
{
	public interface IMessageFilter
	{
		void OnRequestExecuting(Message message);
		void OnResponseExecuting(Message message);
	}
}
