using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore
{
	[Obsolete]
	internal sealed class ObsoleteMessageFilter : IAsyncMessageFilter
	{
		private readonly IMessageFilter _messageFilter;

		public ObsoleteMessageFilter(IMessageFilter messageFilter)
		{
			_messageFilter = messageFilter;
		}

		public Task OnRequestExecuting(Message message)
		{
			_messageFilter.OnRequestExecuting(message);
			return Task.CompletedTask;
		}

		public Task OnResponseExecuting(Message message)
		{
			_messageFilter.OnResponseExecuting(message);
			return Task.CompletedTask;
		}
	}
}
