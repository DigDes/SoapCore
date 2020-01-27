using System;
using System.Linq;
using System.ServiceModel.Channels;
using System.Xml;
using SoapCore.ServiceModel;

namespace SoapCore.Meta
{
	internal class MetaMessage : Message
	{
		private readonly Message _message;
		private readonly ServiceDescription _service;
		private readonly Binding _binding;

		public MetaMessage(Message message, ServiceDescription service, Binding binding)
		{
			_message = message;
			_service = service;
			_binding = binding;
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
			const string WSP_NS = Namespaces.WSP_NS;
			const string HTTP_NS = "http://schemas.microsoft.com/ws/06/2004/policy/http";

			writer.WriteStartElement("wsdl", "definitions", Namespaces.WSDL_NS);
			writer.WriteXmlnsAttribute("wsdl", Namespaces.WSDL_NS);
			writer.WriteXmlnsAttribute("xsd", Namespaces.XMLNS_XSD);
			writer.WriteXmlnsAttribute("msc", "http://schemas.microsoft.com/ws/2005/12/wsdl/contract");
			writer.WriteXmlnsAttribute("wsp", WSP_NS);
			writer.WriteXmlnsAttribute("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
			writer.WriteXmlnsAttribute("http", HTTP_NS);

			// Soap11
			if (Version == MessageVersion.Soap11 || Version == MessageVersion.Soap11WSAddressingAugust2004 || Version == MessageVersion.Soap11WSAddressingAugust2004)
			{
				writer.WriteXmlnsAttribute("soap", Namespaces.SOAP11_NS);
			}

			// Soap12
			else if (Version == MessageVersion.Soap12WSAddressing10 || Version == MessageVersion.Soap12WSAddressingAugust2004)
			{
				writer.WriteXmlnsAttribute("soap12", Namespaces.SOAP12_NS);
			}
			else
			{
				throw new ArgumentOutOfRangeException(nameof(Version), "Unsupported MessageVersion encountered while writing envelope.");
			}

			writer.WriteXmlnsAttribute("tns", _service.Contracts.First().Namespace);
			writer.WriteXmlnsAttribute("wsam", Namespaces.WSAM_NS);
			writer.WriteAttributeString("targetNamespace", _service.Contracts.First().Namespace);
			writer.WriteAttributeString("name", _service.ServiceType.Name);

			if (_binding != null && _binding.HasBasicAuth())
			{
				writer.WriteStartElement("wsp", "Policy", WSP_NS);
				writer.WriteAttributeString("Id", "wsu", $"{_binding.Name}_{_service.Contracts.First().Name}_policy");
				writer.WriteStartElement("wsp", "ExactlyOne", WSP_NS);
				writer.WriteStartElement("wsp", "All", WSP_NS);
				writer.WriteStartElement("http", "BasicAuthentication", HTTP_NS);
				writer.WriteEndElement();
				writer.WriteEndElement();
				writer.WriteEndElement();
				writer.WriteEndElement();
			}
		}

		protected override void OnWriteStartBody(XmlDictionaryWriter writer)
		{
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			_message.WriteBodyContents(writer);
		}
	}
}
