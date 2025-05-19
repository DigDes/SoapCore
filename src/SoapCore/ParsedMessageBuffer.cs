using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Text;

namespace SoapCore
{
	public class ParsedMessageBuffer : MessageBuffer
	{
		private readonly ParsedMessage _originalMessage;

		public ParsedMessageBuffer(ParsedMessage message)
		{
			_originalMessage = new ParsedMessage(message.Headers, message.Properties, message.Version, message.GetBodyAsXDocument(), message.IsEmpty);
		}

		public override int BufferSize => int.MaxValue;

		public override void Close()
		{
			_originalMessage?.Close();
		}

		public override Message CreateMessage()
		{
			return new ParsedMessage(_originalMessage.Headers, _originalMessage.Properties, _originalMessage.Version, _originalMessage.GetBodyAsXDocument(), _originalMessage.IsEmpty);
		}
	}
}
