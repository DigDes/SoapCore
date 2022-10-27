using System;
using System.ServiceModel.Channels;
using System.Xml;
using SoapCore.ServiceModel;

namespace SoapCore.Meta
{
	public class MetaMessage : Message
	{
		private readonly Message _message;
		private readonly ServiceDescription _service;
		private readonly XmlNamespaceManager _xmlNamespaceManager;
		private readonly string _bindingName;
		private readonly bool _hasBasicAuthentication;

		[Obsolete]
		public MetaMessage(Message message, ServiceDescription service, Binding binding, XmlNamespaceManager xmlNamespaceManager)
			: this(message, service, xmlNamespaceManager, binding?.Name, binding.HasBasicAuth())
		{
		}

		public MetaMessage(Message message, ServiceDescription service, XmlNamespaceManager xmlNamespaceManager, string bindingName, bool hasBasicAuthentication)
		{
			_xmlNamespaceManager = xmlNamespaceManager;
			_message = message;
			_service = service;
			_bindingName = bindingName;
			_hasBasicAuthentication = hasBasicAuthentication;
		}

		public override MessageHeaders Headers => _message.Headers;

		public override MessageProperties Properties => _message.Properties;

		public override MessageVersion Version => _message.Version;

		public override bool IsEmpty => _message.IsEmpty;

		public override bool IsFault => _message.IsFault;

		protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement(_xmlNamespaceManager.LookupPrefix(Namespaces.WSDL_NS), "definitions", Namespaces.WSDL_NS);

			// Soap11
			if (Version == MessageVersion.Soap11 || Version == MessageVersion.Soap11WSAddressingAugust2004 || Version == MessageVersion.Soap11WSAddressingAugust2004)
			{
				WriteXmlnsAttribute(writer, Namespaces.SOAP11_NS);
			}

			// Soap12
			else if (Version == MessageVersion.Soap12WSAddressing10 || Version == MessageVersion.Soap12WSAddressingAugust2004)
			{
				WriteXmlnsAttribute(writer, Namespaces.SOAP12_NS);
			}
			else
			{
				throw new ArgumentOutOfRangeException(nameof(Version), "Unsupported MessageVersion encountered while writing envelope.");
			}

			WriteXmlnsAttribute(writer, _service.GeneralContract.Namespace);
			WriteXmlnsAttribute(writer, Namespaces.XMLNS_XSD);
			WriteXmlnsAttribute(writer, Namespaces.HTTP_NS);
			WriteXmlnsAttribute(writer, Namespaces.MSC_NS);
			WriteXmlnsAttribute(writer, Namespaces.WSP_NS);
			WriteXmlnsAttribute(writer, Namespaces.WSU_NS);
			WriteXmlnsAttribute(writer, Namespaces.WSAM_NS);
			writer.WriteAttributeString("targetNamespace", _service.GeneralContract.Namespace);
			writer.WriteAttributeString("name", _service.ServiceName);
			WriteXmlnsAttribute(writer, Namespaces.WSDL_NS);

			if (_hasBasicAuthentication)
			{
				writer.WriteStartElement("Policy", Namespaces.WSP_NS);
				writer.WriteAttributeString("Id", _xmlNamespaceManager.LookupPrefix(Namespaces.WSU_NS), $"{_bindingName}_{_service.GeneralContract.Name}_policy");
				writer.WriteStartElement("ExactlyOne", Namespaces.WSP_NS);
				writer.WriteStartElement("All", Namespaces.WSP_NS);
				writer.WriteStartElement("BasicAuthentication", Namespaces.HTTP_NS);
				writer.WriteStartElement("wsaw", "UsingAddressing", Namespaces.WSAW_NS);
				writer.WriteEndElement();
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

		protected override void OnClose()
		{
			_message.Close();
			base.OnClose();
		}

		private void WriteXmlnsAttribute(XmlDictionaryWriter writer, string namespaceUri)
		{
			var prefix = string.IsNullOrEmpty(namespaceUri) ? null : _xmlNamespaceManager.LookupPrefix(namespaceUri);
			writer.WriteXmlnsAttribute(prefix, namespaceUri);
		}
	}
}
