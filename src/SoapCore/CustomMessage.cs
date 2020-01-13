using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore
{
	public class CustomMessage : Message
	{

		public CustomMessage() { }

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
				writer.WriteStartElement("s", "Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
			}
			else
			{
				writer.WriteStartElement("s", "Envelope", "http://www.w3.org/2003/05/soap-envelope");
			}

			writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
			writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			Message.WriteBodyContents(writer);
		}
	}
}
