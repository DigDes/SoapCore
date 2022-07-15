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

		public XmlNamespaceManager NamespaceManager { get; internal set; }

		public System.Collections.Generic.Dictionary<string, string> AdditionalEnvelopeXmlnsAttributes { get; internal set; }

		public override MessageHeaders Headers => Message.Headers;

		public override MessageProperties Properties => Message.Properties;

		public override MessageVersion Version => Message.Version;

		public override bool IsEmpty => Message.IsEmpty;

		public override bool IsFault => Message.IsFault;

		protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
		{
			writer.WriteStartDocument();
			var prefix = Version.Envelope.NamespacePrefix(NamespaceManager);
			writer.WriteStartElement(prefix, "Envelope", Version.Envelope.Namespace());
			writer.WriteXmlnsAttribute(prefix, Version.Envelope.Namespace());

			var xsdPrefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, "xsd", Namespaces.XMLNS_XSD);
			writer.WriteXmlnsAttribute(xsdPrefix, Namespaces.XMLNS_XSD);

			var xsiPrefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, "xsi", Namespaces.XMLNS_XSI);
			writer.WriteXmlnsAttribute(xsiPrefix, Namespaces.XMLNS_XSI);

			if (AdditionalEnvelopeXmlnsAttributes != null)
			{
				foreach (var rec in AdditionalEnvelopeXmlnsAttributes)
				{
					writer.WriteXmlnsAttribute(rec.Key, rec.Value);
				}
			}
		}

		protected override void OnWriteStartHeaders(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement(Version.Envelope.NamespacePrefix(NamespaceManager), "Header", Version.Envelope.Namespace());
		}

		protected override void OnWriteStartBody(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement(Version.Envelope.NamespacePrefix(NamespaceManager), "Body", Version.Envelope.Namespace());
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			Message.WriteBodyContents(writer);
		}

		protected override void OnClose()
		{
			Message.Close();
			base.OnClose();
		}
	}
}
