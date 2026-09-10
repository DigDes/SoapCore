using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using SoapCore.Extensibility;

namespace SoapCore.Tests.DuplicateFault
{
	public class CountingFaultExceptionTransformer : IFaultExceptionTransformer
	{
		private int _provideFaultCallCount;

		public int ProvideFaultCallCount => _provideFaultCallCount;

		public Message ProvideFault(Exception exception, MessageVersion messageVersion, Message requestMessage, ConcurrentXmlNamespaceLookup xmlNamespaceLookup)
		{
			_provideFaultCallCount++;

			var faultException = new FaultException(new FaultReason(exception.Message), new FaultCode("Sender"), null);
			var messageFault = faultException.CreateMessageFault();
			var bodyWriter = new MessageFaultBodyWriter(messageFault, messageVersion);

			return Message.CreateMessage(messageVersion, null, bodyWriter);
		}
	}
}
