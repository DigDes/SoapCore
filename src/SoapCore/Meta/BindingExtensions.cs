using System;
using System.Net;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace SoapCore.Meta
{
	[Obsolete]
	internal static class BindingExtensions
	{
		public static bool HasBasicAuth(this Binding binding)
		{
			var transportBindingElement = binding?.CreateBindingElements().Find<HttpTransportBindingElement>();

			if (transportBindingElement != null)
			{
				return transportBindingElement.AuthenticationScheme == AuthenticationSchemes.Basic;
			}

			return false;
		}

		public static SoapEncoderOptions[] ToEncoderOptions(this Binding binding)
		{
			var elements = binding.CreateBindingElements().FindAll<MessageEncodingBindingElement>();
			var encoderOptions = new SoapEncoderOptions[elements.Count];

			for (var i = 0; i < encoderOptions.Length; i++)
			{
				var encoderOption = new SoapEncoderOptions
				{
					MessageVersion = elements[i].MessageVersion,
					WriteEncoding = DefaultEncodings.UTF8,
					ReaderQuotas = XmlDictionaryReaderQuotas.Max
				};

				if (elements[i] is TextMessageEncodingBindingElement textMessageEncodingBindingElement)
				{
					encoderOption.WriteEncoding = textMessageEncodingBindingElement.WriteEncoding;
					encoderOption.ReaderQuotas = textMessageEncodingBindingElement.ReaderQuotas;
				}

				encoderOptions[i] = encoderOption;
			}

			return encoderOptions;
		}
	}
}
