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
            _message.WriteBodyContents(writer);
        }
    }
}
