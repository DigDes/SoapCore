using System;
using System.ServiceModel.Channels;
using System.Xml;
using SoapCore.Extensibility;

namespace SoapCore
{
	/// <summary>
	/// The default implementation of the fault provider when an unexpected exception occurs. This can be replaced or
	/// extended by registering your own IFaultExceptionTransformer in the service collection on startup.
	/// </summary>
	/// <typeparam name="T_MESSAGE">The message type.</typeparam>
	public class DefaultFaultExceptionTransformer<T_MESSAGE> : IFaultExceptionTransformer
		where T_MESSAGE : CustomMessage, new()
	{
		private readonly ExceptionTransformer _exceptionTransformer;

		public DefaultFaultExceptionTransformer()
		{
			_exceptionTransformer = null;
		}

		public DefaultFaultExceptionTransformer(ExceptionTransformer exceptionTransformer)
		{
			_exceptionTransformer = exceptionTransformer;
		}

		public Message ProvideFault(Exception exception, MessageVersion messageVersion, Message requestMessage, XmlNamespaceManager xmlNamespaceManager)
		{
			var bodyWriter = _exceptionTransformer == null ?
				new FaultBodyWriter(exception, messageVersion) :
				new FaultBodyWriter(exception, messageVersion, faultStringOverride: _exceptionTransformer.Transform(exception));

			var soapCoreFaultMessage = Message.CreateMessage(messageVersion, null, bodyWriter);

			T_MESSAGE customMessage = new T_MESSAGE
			{
				Message = soapCoreFaultMessage,
				NamespaceManager = xmlNamespaceManager
			};

			return customMessage;
		}
	}
}
