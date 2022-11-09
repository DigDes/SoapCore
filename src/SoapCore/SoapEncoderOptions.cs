using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace SoapCore
{
	public class SoapEncoderOptions
	{
		public MessageVersion MessageVersion { get; set; } = MessageVersion.Soap11;

		public Encoding WriteEncoding { get; set; } = DefaultEncodings.UTF8;

		public bool OverwriteResponseContentType { get; set; }
		public XmlDictionaryReaderQuotas ReaderQuotas { get; set; } = XmlDictionaryReaderQuotas.Max;
		public string BindingName { get; set; } = null;
		public string PortName { get; set; } = null;

		public XmlNamespaceManager XmlNamespaceOverrides { get; set; } = null;

		public int MaxSoapHeaderSize { get; set; } = MessageEncoder.SoapMessageEncoderDefaults.MaxSoapHeaderSizeDefault;

		internal static SoapEncoderOptions[] ToArray(SoapEncoderOptions options)
		{
			return options is null ? null : new[] { options };
		}
	}
}
