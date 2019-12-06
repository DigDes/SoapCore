using System;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using SoapCore.Extensibility;

namespace SoapCore.Tests.FaultExceptionTransformer
{
	public class TestFaultExceptionTransformer : IFaultExceptionTransformer
	{
		public Message ProvideFault(Exception exception, MessageVersion messageVersion)
		{
			var fault = new TestFault
			{
				Message = exception.Message,
				AdditionalProperty = "foo:bar"
			};

			var faultException = new FaultException<TestFault>(fault, new FaultReason(exception.Message), new FaultCode(nameof(TestFault)), nameof(TestFault));

			var messageFault = faultException.CreateMessageFault();
			var bodyWriter = new MessageFaultBodyWriter(messageFault, messageVersion);
			var faultMessage = Message.CreateMessage(messageVersion, null, bodyWriter);

			faultMessage.Properties.Add(HttpResponseMessageProperty.Name, new HttpResponseMessageProperty { StatusCode = HttpStatusCode.OK, StatusDescription = "test description" });

			return faultMessage;
		}
	}
}
