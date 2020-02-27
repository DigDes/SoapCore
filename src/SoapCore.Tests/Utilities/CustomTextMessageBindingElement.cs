using System;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore.Tests.Utilities
{
	public class CustomTextMessageBindingElement : MessageEncodingBindingElement //, IWsdlExportExtension
	{
		private readonly XmlDictionaryReaderQuotas _readerQuotas;
		private MessageVersion _msgVersion;
		private string _mediaType;
		private string _encoding;

		public CustomTextMessageBindingElement(string encoding, string mediaType, MessageVersion msgVersion)
		{
			if (encoding == null)
			{
				throw new ArgumentNullException("encoding");
			}

			if (mediaType == null)
			{
				throw new ArgumentNullException("mediaType");
			}

			if (msgVersion == null)
			{
				throw new ArgumentNullException("msgVersion");
			}

			_msgVersion = msgVersion;
			_mediaType = mediaType;
			_encoding = encoding;
			_readerQuotas = new XmlDictionaryReaderQuotas();
		}

		public CustomTextMessageBindingElement(string encoding, string mediaType)
			: this(encoding, mediaType, MessageVersion.Soap11)
		{
		}

		public CustomTextMessageBindingElement(string encoding)
			: this(encoding, "text/xml")
		{
		}

		public CustomTextMessageBindingElement()
			: this("UTF-8")
		{
		}

		private CustomTextMessageBindingElement(CustomTextMessageBindingElement binding)
			: this(binding.Encoding, binding.MediaType, binding.MessageVersion)
		{
			_readerQuotas = new XmlDictionaryReaderQuotas();
			binding.ReaderQuotas.CopyTo(_readerQuotas);
		}

		public override MessageVersion MessageVersion
		{
			get
			{
				return _msgVersion;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_msgVersion = value;
			}
		}

		public string MediaType
		{
			get
			{
				return _mediaType;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_mediaType = value;
			}
		}

		public string Encoding
		{
			get
			{
				return _encoding;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_encoding = value;
			}
		}

		// This encoder does not enforces any quotas for the unsecure messages. The
		// quotas are enforced for the secure portions of messages when this encoder
		// is used in a binding that is configured with security.
		public XmlDictionaryReaderQuotas ReaderQuotas
		{
			get { return _readerQuotas; }
		}

		public override MessageEncoderFactory CreateMessageEncoderFactory()
		{
			return new CustomTextMessageEncoderFactory(MediaType, Encoding, MessageVersion);
		}

		public override BindingElement Clone()
		{
			return new CustomTextMessageBindingElement(this);
		}

		public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
		{
			if (context == null)
			{
				throw new ArgumentNullException("context");
			}

			context.BindingParameters.Add(this);
			return context.BuildInnerChannelFactory<TChannel>();
		}

		public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
		{
			if (context == null)
			{
				throw new ArgumentNullException("context");
			}

			return context.CanBuildInnerChannelFactory<TChannel>();
		}

		// public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
		// {
		// 	if (context == null)
		// 		throw new ArgumentNullException("context");
		//
		// 	context.BindingParameters.Add(this);
		// 	return context.BuildInnerChannelListener<TChannel>();
		// }
		//
		// public override bool CanBuildChannelListener<TChannel>(BindingContext context)
		// {
		// 	if (context == null)
		// 		throw new ArgumentNullException("context");
		//
		// 	context.BindingParameters.Add(this);
		// 	return context.CanBuildInnerChannelListener<TChannel>();
		// }
		public override T GetProperty<T>(BindingContext context)
		{
			if (typeof(T) == typeof(XmlDictionaryReaderQuotas))
			{
				return (T)(object)_readerQuotas;
			}
			else
			{
				return base.GetProperty<T>(context);
			}
		}

		// #region IWsdlExportExtension Members
		//
		// void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext context)
		// {
		// }
		//
		// void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext context)
		// {
		// 	// The MessageEncodingBindingElement is responsible for ensuring that the WSDL has the correct
		// 	// SOAP version. We can delegate to the WCF implementation of TextMessageEncodingBindingElement for
		// 	TextMessageEncodingBindingElement mebe = new TextMessageEncodingBindingElement();
		// 	mebe.MessageVersion = msgVersion;
		// 	((IWsdlExportExtension) mebe).ExportEndpoint(exporter, context);
		// }
		//
		// #endregion
	}
}
