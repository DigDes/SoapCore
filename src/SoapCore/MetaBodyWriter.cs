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
        public MetaBodyWriter(ServiceDescription service) : base(isBuffered:true)
        {
            _service = service;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {

            writer.WriteEndElement();
            writer.WriteStartElement("wsdl:message");
            writer.WriteEndElement();
            writer.WriteStartElement("wsdl:portType");
            writer.WriteEndElement();

            AddBinding(writer);

            writer.WriteStartElement("wsdl:service");
        }

//<wsdl:binding name = "BasicHttpBinding_IRoutingService" type="tns:IRoutingService">
//<soap:binding transport = "http://schemas.xmlsoap.org/soap/http" />
//< wsdl:operation name = "Ping" >
// < soap:operation soapAction = "http://tempuri.org/IRoutingService/Ping" style="document"/>
//<wsdl:input>
//<soap:body use = "literal" />
//</ wsdl:input>
//<wsdl:output>
//<soap:body use = "literal" />
//</ wsdl:output>
//</wsdl:operation>
//</wsdl:binding>
        private void AddBinding(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:binding");
            writer.WriteAttributeString("name", "BasicHttpBinding_" + _service.Contracts.First().Name);
            writer.WriteAttributeString("type", "tns:" + _service.Contracts.First().Name);

            writer.WriteEndElement();
        }
    }
}
