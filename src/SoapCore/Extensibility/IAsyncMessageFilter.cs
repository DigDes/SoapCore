using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace SoapCore.Extensibility
{
	public interface IAsyncMessageFilter
	{
		Task OnRequestExecuting(Message message);
		Task OnResponseExecuting(Message message);
	}
}
