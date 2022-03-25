using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace SoapCore
{
	public class SoapEncoderOptions
	{
		public MessageVersion MessageVersion { get; set; } = MessageVersion.Soap11;
		public Encoding WriteEncoding { get; set; } = DefaultEncodings.UTF8;
		public XmlDictionaryReaderQuotas ReaderQuotas { get; set; } = XmlDictionaryReaderQuotas.Max;
		public string BindingName { get; set; } = null;
		public string PortName { get; set; } = null;

		public XmlNamespaceManager XmlNamespaceOverrides { get; set; } = null;

		internal static SoapEncoderOptions[] ToArray(SoapEncoderOptions options)
		{
			return options is null ? null : new[] { options };
		}
	}
}
