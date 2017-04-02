using System;
using System.Linq;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore
{
    public class MetaBodyWriter : BodyWriter
    {
        private const string XMLNS_XS = "http://www.w3.org/2001/XMLSchema";
        private const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";

        private readonly ServiceDescription _service;
        private readonly string _baseUrl;

        private string BindingName => "BasicHttpBinding_" + _service.Contracts.First().Name;
        private string BindingType => _service.Contracts.First().Name;
        private string PortName => "BasicHttpBinding_" + _service.Contracts.First().Name;
        private string TargetNameSpace => _service.Contracts.First().Namespace;

        public MetaBodyWriter(ServiceDescription service, string baseUrl) : base(isBuffered: true)
        {
            _service = service;
            _baseUrl = baseUrl;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            AddTypes(writer);

            AddMessage(writer);

            AddPortType(writer);

            AddBinding(writer);

            AddService(writer);
        }

        private void AddTypes(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("xs:schema");
            writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
            writer.WriteAttributeString("elementFormDefault", "qualified");
            writer.WriteAttributeString("targetNamespace", TargetNameSpace);

            foreach (var operation in _service.Operations)
            {
                // input parameters of operation
                writer.WriteStartElement("xs:element");
                writer.WriteAttributeString("name", operation.Name);
                writer.WriteStartElement("xs:complexType");
                writer.WriteStartElement("xs:sequence");

                foreach (var parameter in operation.DispatchMethod.GetParameters().Where(x => !x.IsOut && !x.ParameterType.IsByRef))
                {
                    string xsTypename = ResolveType(parameter.ParameterType.Name);
                    writer.WriteStartElement("xs:element");
                    writer.WriteAttributeString("name", parameter.Name);
                    writer.WriteAttributeString("minOccurs", "0");
                    writer.WriteAttributeString("nillable", "true");
                    writer.WriteAttributeString("type", xsTypename);
                    writer.WriteEndElement(); // xs:element
                }

                writer.WriteEndElement(); // xs:sequence
                writer.WriteEndElement(); // xs:complexType
                writer.WriteEndElement(); // xs:element

                // output parameter / return of operation
                writer.WriteStartElement("xs:element");
                writer.WriteAttributeString("name", operation.Name + "Response");
                writer.WriteStartElement("xs:complexType");
                writer.WriteStartElement("xs:sequence");

                string xsReturnTypename = ResolveType(operation.DispatchMethod.ReturnType.Name);
                writer.WriteStartElement("xs:element");
                writer.WriteAttributeString("name", operation.Name + "Result");
                writer.WriteAttributeString("minOccurs", "0");
                writer.WriteAttributeString("nillable", "true");
                writer.WriteAttributeString("type", xsReturnTypename);
                writer.WriteEndElement(); // xs:element

                writer.WriteEndElement(); // xs:sequence
                writer.WriteEndElement(); // xs:complexType
                writer.WriteEndElement(); // xs:element
            }

            writer.WriteEndElement(); // xs:schema

            writer.WriteEndElement(); // wsdl:types
        }

        private void AddMessage(XmlDictionaryWriter writer)
        {
            foreach (var operation in _service.Operations)
            {
                // input
                writer.WriteStartElement("wsdl:message");
                writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_InputMessage");
                writer.WriteStartElement("wsdl:part");
                writer.WriteAttributeString("name", "parameters");
                writer.WriteAttributeString("element", "tns:" + operation.Name);
                writer.WriteEndElement(); // wsdl:part
                writer.WriteEndElement(); // wsdl:message
                // output
                writer.WriteStartElement("wsdl:message");
                writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_OutputMessage");
                writer.WriteStartElement("wsdl:part");
                writer.WriteAttributeString("name", "parameters");
                writer.WriteAttributeString("element", "tns:" + operation.Name + "Response");
                writer.WriteEndElement(); // wsdl:part
                writer.WriteEndElement(); // wsdl:message
            }
        }

        private void AddPortType(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:portType");
            writer.WriteAttributeString("name", BindingType);
            foreach (var operation in _service.Operations)
            {
                writer.WriteStartElement("wsdl:operation");
                writer.WriteAttributeString("name", operation.Name);
                writer.WriteStartElement("wsdl:input");
                writer.WriteAttributeString("wsaw:Action", operation.SoapAction);
                writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_InputMessage");
                writer.WriteEndElement(); // wsdl:input
                writer.WriteStartElement("wsdl:output");
                writer.WriteAttributeString("wsaw:Action", operation.SoapAction + "Response");
                writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_OutputMessage");
                writer.WriteEndElement(); // wsdl:output
                writer.WriteEndElement(); // wsdl:operation
            }
            writer.WriteEndElement(); // wsdl:portType
        }

        private void AddBinding(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:binding");
            writer.WriteAttributeString("name", BindingName);
            writer.WriteAttributeString("type", "tns:" + BindingType);

            writer.WriteStartElement("soap:binding");
            writer.WriteAttributeString("transport", TRANSPORT_SCHEMA);
            writer.WriteEndElement(); // soap:binding

            foreach (var operation in _service.Operations)
            {
                writer.WriteStartElement("wsdl:operation");
                writer.WriteAttributeString("name", operation.Name);

                writer.WriteStartElement("soap:operation");
                writer.WriteAttributeString("soapAction", operation.SoapAction);
                writer.WriteAttributeString("style", "document");
                writer.WriteEndElement(); // soap:operation

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

                writer.WriteEndElement(); // wsdl:operation
            }

            writer.WriteEndElement(); // wsdl:binding
        }

        private void AddService(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:service");
            writer.WriteAttributeString("name", _service.ServiceType.Name);

            writer.WriteStartElement("wsdl:port");
            writer.WriteAttributeString("name", PortName);
            writer.WriteAttributeString("binding", "tns:" + BindingName);

            writer.WriteStartElement("soap:address");

            writer.WriteAttributeString("location", _baseUrl);
            writer.WriteEndElement(); // soap:address

            writer.WriteEndElement(); // wsdl:port
        }

        private string ResolveType(string typeName)
        {
            string resolvedType = null;

            switch (typeName)
            {
                case "String":
                    resolvedType = "xs:string";
                    break;

                case "Int32":
                    resolvedType = "xs:int";
                    break;
            }

            if (String.IsNullOrEmpty(resolvedType))
            {
                throw new ArgumentException($".NET type {typeName} cannot be resolved into XML schema type");
            }

            return resolvedType;
        }

    }
}
