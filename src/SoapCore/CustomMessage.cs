using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore
{
	public class CustomMessage : Message
	{
		public CustomMessage()
		{
		}

		public CustomMessage(Message message)
		{
			Message = message;
		}

		public Message Message { get; internal set; }
		public override MessageHeaders Headers
		{
			get { return Message.Headers; }
		}

		public override MessageProperties Properties
		{
			get { return Message.Properties; }
		}

		public override MessageVersion Version
		{
			get { return Message.Version; }
		}

		protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
		{
			writer.WriteStartDocument();
			if (Message.Version.Envelope == EnvelopeVersion.Soap11)
			{
				writer.WriteStartElement("s", "Envelope", Namespaces.SOAP11_ENVELOPE_NS);
			}
			else
			{
				writer.WriteStartElement("s", "Envelope", Namespaces.SOAP12_ENVELOPE_NS);
			}

			writer.WriteXmlnsAttribute("xsd", Namespaces.XMLNS_XSD);
			writer.WriteXmlnsAttribute("xsi", Namespaces.XMLNS_XSI);
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			Message.WriteBodyContents(writer);
		}
	}
}
