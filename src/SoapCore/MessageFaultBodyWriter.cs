using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore
{
	/// <summary>
	/// BodyWriter implementation that formats MessageFault messages (from FaultException)
	/// </summary>
	public class MessageFaultBodyWriter : BodyWriter
	{
		private readonly MessageFault _fault;
		private readonly MessageVersion _messageVersion;

		public MessageFaultBodyWriter(MessageFault fault, MessageVersion messageVersion, bool isBuffered = true) : base(isBuffered)
		{
			_fault = fault;
			_messageVersion = messageVersion;
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			// This uses reflection to find the WriteTo method.
			// For some reason, even though its in the assembly, its not exposed in the .NET Standard API
			var writeToMethod = _fault.GetType().GetMethod("WriteTo", new[] { typeof(XmlDictionaryWriter), typeof(EnvelopeVersion) });
			writeToMethod.Invoke(_fault, new object[] { writer, _messageVersion.Envelope });
		}
	}
}
