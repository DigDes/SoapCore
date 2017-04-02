using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
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
            //writer.WriteAttributeString("xmlns:msc", "http://schemas.microsoft.com/ws/2005/12/wsdl/contract");
            writer.WriteAttributeString("xmlns:soap", "http://schemas.xmlsoap.org/wsdl/soap/");
            //writer.WriteAttributeString("xmlns:soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
            //writer.WriteAttributeString("xmlns:soapenc", "http://schemas.xmlsoap.org/soap/encoding/");
            //writer.WriteAttributeString("xmlns:wsa", "http://schemas.xmlsoap.org/ws/2004/08/addressing");
            //writer.WriteAttributeString("xmlns:wsam", "http://www.w3.org/2007/05/addressing/metadata");
            //writer.WriteAttributeString("xmlns:wsap", "http://schemas.xmlsoap.org/ws/2004/08/addressing/policy");
            //writer.WriteAttributeString("xmlns:wsaw", "http://www.w3.org/2006/05/addressing/wsdl");
            //writer.WriteAttributeString("xmlns:wsp", "http://schemas.xmlsoap.org/ws/2004/09/policy");
            //writer.WriteAttributeString("xmlns:wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            //writer.WriteAttributeString("xmlns:wsx", "http://schemas.xmlsoap.org/ws/2004/09/mex");
            writer.WriteAttributeString("xmlns:tns", "http://tempuri.org/");
            writer.WriteAttributeString("targetNamespace", _service.Contracts.First().Name);
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
