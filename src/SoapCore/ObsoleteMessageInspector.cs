using System;
using System.ServiceModel.Channels;
using SoapCore.Extensibility;
using SoapCore.ServiceModel;

namespace SoapCore
{
	[Obsolete]
	internal sealed class ObsoleteMessageInspector : IMessageInspector2
	{
		private readonly IMessageInspector _inner;

		public ObsoleteMessageInspector(IMessageInspector inner)
		{
			_inner = inner;
		}

		public object AfterReceiveRequest(ref Message message, ServiceDescription serviceDescription)
		{
			return _inner.AfterReceiveRequest(ref message);
		}

		public void BeforeSendReply(ref Message reply, ServiceDescription serviceDescription, object correlationState)
		{
			_inner.BeforeSendReply(ref reply, correlationState);
		}
	}
}
