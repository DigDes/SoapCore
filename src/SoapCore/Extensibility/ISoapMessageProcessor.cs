using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SoapCore.Extensibility
{
	public interface ISoapMessageProcessor
	{
		public Task<Message> ProcessMessage(Message message, HttpContext context, Func<Message, Task<Message>> next);
	}

	internal class LambdaSoapMessageProcessor : ISoapMessageProcessor
	{
		private Func<Message, HttpContext, Func<Message, Task<Message>>, Task<Message>> _processMessage;

		internal LambdaSoapMessageProcessor(Func<Message, HttpContext, Func<Message, Task<Message>>, Task<Message>> processMessage)
		{
			_processMessage = processMessage;
		}

		public async Task<Message> ProcessMessage(Message message, HttpContext context, Func<Message, Task<Message>> next)
		{
			return await _processMessage(message, context, next);
		}
	}
}
