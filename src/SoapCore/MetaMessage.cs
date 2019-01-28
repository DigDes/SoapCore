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

			if (Version == MessageVersion.Soap11 || Version == MessageVersion.Soap11WSAddressingAugust2004 || Version == MessageVersion.Soap11WSAddressingAugust2004) // Soap11
			{
				writer.WriteAttributeString("xmlns:soap", "http://schemas.xmlsoap.org/wsdl/soap/");
            }
			else if (Version == MessageVersion.Soap12WSAddressing10 || Version == MessageVersion.Soap12WSAddressingAugust2004) // Soap12
			{
				writer.WriteAttributeString("xmlns:soap", "http://schemas.xmlsoap.org/wsdl/soap12/");
            }
			else
			{
				throw new Exception("Unsupported MessageVersion encountered while writing envelope: " + Version);
			}

			writer.WriteAttributeString("xmlns:tns", _service.Contracts.First().Namespace);
			writer.WriteAttributeString("xmlns:wsam", "http://www.w3.org/2007/05/addressing/metadata");
			writer.WriteAttributeString("targetNamespace", _service.Contracts.First().Namespace);
			writer.WriteAttributeString("name", _service.ServiceType.Name);
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
