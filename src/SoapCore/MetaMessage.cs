using System.Linq;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore
{
    public class MetaMessage : Message
    {
        private readonly Message _message;
        private readonly ServiceDescription _service;

        public MetaMessage(Message message, ServiceDescription service)
        {
            _message = message;
            _service = service;
        }

        /// <summary>
        /// override to replace s:Envelope
        /// </summary>
        /// <param name="writer"></param>
        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl", "definitions", "http://schemas.xmlsoap.org/wsdl/");
            writer.WriteAttributeString("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
            writer.WriteAttributeString("xmlns:soap", "http://schemas.xmlsoap.org/wsdl/soap/");
            writer.WriteAttributeString("xmlns:tns", _service.Contracts.First().Namespace);
            writer.WriteAttributeString("targetNamespace", _service.Contracts.First().Namespace);
            writer.WriteAttributeString("name", _service.ServiceType.Name);
        }

        /// <summary>
        /// override to replace s:Body
        /// </summary>
        /// <param name="writer"></param>
        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:types");
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            _message.WriteBodyContents(writer);
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
    }
}
