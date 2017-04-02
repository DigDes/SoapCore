using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;

namespace SoapCore
{
    public class MetaBodyWriter : BodyWriter
    {
        private readonly ServiceDescription _service;
        private readonly string _baseUrl;
        public MetaBodyWriter(ServiceDescription service,string baseUrl) : base(isBuffered:true)
        {
            _service = service;
            _baseUrl = baseUrl;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {

            writer.WriteEndElement();
            writer.WriteStartElement("wsdl:message");
            writer.WriteEndElement();
            writer.WriteStartElement("wsdl:portType");
            writer.WriteEndElement();

            AddBinding(writer);
            AddService(writer);
        }

        private void AddBinding(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:binding");
            writer.WriteAttributeString("name", "BasicHttpBinding_" + _service.Contracts.First().Name);
            writer.WriteAttributeString("type", "tns:" + _service.Contracts.First().Name);

            writer.WriteStartElement("soap:binding");
            writer.WriteAttributeString("transport", "http://schemas.xmlsoap.org/soap/http");
            writer.WriteEndElement(); // soap:binding

            foreach(var operation in _service.Operations)
            {
                writer.WriteStartElement("wsdl:operation");
                writer.WriteAttributeString("name", operation.Name);

                writer.WriteStartElement("soap:operation");
                writer.WriteAttributeString("soapAction", _service.Contracts.First().Namespace + _service.Contracts.First().Name + "/" + operation.Name);
                writer.WriteAttributeString("style", "document");

                writer.WriteStartElement("wsdl:input");
                writer.WriteStartElement("soap:body");
                writer.WriteAttributeString("use", "literal");
                writer.WriteEndElement(); // soap:body
                writer.WriteEndElement(); // wsdl:input

                writer.WriteStartElement("wsdl:output");
                writer.WriteStartElement("soap:body");
                writer.WriteAttributeString("use", "literal");
                writer.WriteEndElement(); // soap:body
                writer.WriteEndElement(); // wsdl:output

                writer.WriteEndElement(); // soap:operation

                writer.WriteEndElement(); // wsdl:operation
            }

            writer.WriteEndElement(); // wsdl:binding
        }

        private void AddService(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:service");
            writer.WriteAttributeString("name", _service.ServiceType.Name);

            writer.WriteStartElement("wsdl:port");
            writer.WriteAttributeString("name", "BasicHttpBinding_" + _service.Contracts.First().Name);
            writer.WriteAttributeString("binding", "tns:BasicHttpBinding_" + _service.Contracts.First().Name);

            writer.WriteStartElement("soap:address");

            writer.WriteAttributeString("location", _baseUrl);
            writer.WriteEndElement(); // soap:address

            writer.WriteEndElement(); // wsdl:port

            // WriteEndElement not required
        }
    }
}
