using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel.Channels;
using System.Text;

namespace SoapCore.Tests
{
	internal class MockMessageEncoder : System.ServiceModel.Channels.MessageEncoder
	{
		private bool _didWriteMessage = false;
		public bool DidWriteMessage
		{
			get
			{
				return _didWriteMessage;
			}
		}

		public override string ContentType { get; }
		public override string MediaType { get; }
		public override MessageVersion MessageVersion => MessageVersion.Soap12WSAddressing10;

		public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType) => throw new NotImplementedException();
		public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType) => throw new NotImplementedException();
		public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset) => throw new NotImplementedException();
		public override void WriteMessage(Message message, Stream stream)
		{
			_didWriteMessage = true;
		}
	}
}
