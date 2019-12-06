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
			writer.WriteStartElement("wsdl", "definitions", "http://schemas.xmlsoap.org/wsdl/");
			writer.WriteAttributeString("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
			writer.WriteAttributeString("xmlns:msc", "http://schemas.microsoft.com/ws/2005/12/wsdl/contract");
			writer.WriteAttributeString("xmlns:wsp", "http://schemas.xmlsoap.org/ws/2004/09/policy");
			writer.WriteAttributeString("xmlns:wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
			writer.WriteAttributeString("xmlns:http", "http://schemas.microsoft.com/ws/06/2004/policy/http");

			// Soap11
			if (Version == MessageVersion.Soap11 || Version == MessageVersion.Soap11WSAddressingAugust2004 || Version == MessageVersion.Soap11WSAddressingAugust2004)
			{
				writer.WriteAttributeString("xmlns:soap", "http://schemas.xmlsoap.org/wsdl/soap/");
			}

			// Soap12
			else if (Version == MessageVersion.Soap12WSAddressing10 || Version == MessageVersion.Soap12WSAddressingAugust2004)
			{
				writer.WriteAttributeString("xmlns:soap", "http://schemas.xmlsoap.org/wsdl/soap12/");
			}
			else
			{
				throw new ArgumentOutOfRangeException(nameof(Version), "Unsupported MessageVersion encountered while writing envelope.");
			}

			writer.WriteAttributeString("xmlns:tns", _service.Contracts.First().Namespace);
			writer.WriteAttributeString("xmlns:wsam", "http://www.w3.org/2007/05/addressing/metadata");
			writer.WriteAttributeString("targetNamespace", _service.Contracts.First().Namespace);
			writer.WriteAttributeString("name", _service.ServiceType.Name);

			if (_binding != null && _binding.HasBasicAuth())
			{
				writer.WriteStartElement("wsp:Policy");
				writer.WriteAttributeString("wsu:Id", $"{_binding.Name}_{_service.Contracts.First().Name}_policy");
				writer.WriteStartElement("wsp:ExactlyOne");
				writer.WriteStartElement("wsp:All");
				writer.WriteStartElement("http:BasicAuthentication");
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
