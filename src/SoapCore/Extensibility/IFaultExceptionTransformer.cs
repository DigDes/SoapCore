using System;
using System.ServiceModel.Channels;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// Allows for applications to define their own fault message type
	/// </summary>
	public interface IFaultExceptionTransformer
	{
		/// <summary>
		/// Transforms a provided exception to a formatted SOAP Message.
		///
		/// If porting an existing application that uses FaultException CreateMessageFault(),
		/// you will need to format it by creating an instance of MessageFaultBodyWriter
		/// and passing that to Message.CreateMessage
		/// </summary>
		/// <param name="exception">Exception to transform</param>
		/// <param name="messageVersion">SOAP message version</param>
		/// <returns>Fully formatted SOAP Message</returns>
		/// <seealso cref="MessageFaultBodyWriter"/>
		Message ProvideFault(Exception exception, MessageVersion messageVersion);
	}
}
