using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore
{
	public class CustomMessage : Message
	{
		private readonly Message _message;

		public CustomMessage(Message message)
		{
			_message = message;
		}

		public override MessageHeaders Headers
		{
			get { return _message.Headers; }
		}

		public override MessageProperties Properties
		{
			get { return _message.Properties; }
		}

		public override MessageVersion Version
		{
			get { return _message.Version; }
		}

		protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
		{
			writer.WriteStartDocument();
			if (_message.Version.Envelope == EnvelopeVersion.Soap11)
			{
				writer.WriteStartElement("s", "Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
			}
			else
			{
				writer.WriteStartElement("s", "Envelope", "http://www.w3.org/2003/05/soap-envelope");
			}

			writer.WriteAttributeString("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
			writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			_message.WriteBodyContents(writer);
		}
	}
}
