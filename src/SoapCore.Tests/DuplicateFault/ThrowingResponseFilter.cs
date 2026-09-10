using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using SoapCore.Extensibility;

namespace SoapCore.Tests.DuplicateFault
{
	public class ThrowingResponseFilter : IAsyncMessageFilter
	{
		public Task OnRequestExecuting(Message message)
		{
			return Task.CompletedTask;
		}

		public Task OnResponseExecuting(Message message)
		{
			throw new InvalidOperationException("The response is not acceptable.");
		}
	}
}
