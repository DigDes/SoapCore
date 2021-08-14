using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace SoapCore
{
	public class SoapEncoderOptions
	{
		public MessageVersion MessageVersion { get; set; } = MessageVersion.Soap11;
		public Encoding WriteEncoding { get; set; } = Encoding.UTF8;
		public XmlDictionaryReaderQuotas ReaderQuotas { get; set; } = XmlDictionaryReaderQuotas.Max;

		internal static SoapEncoderOptions[] ToArray(SoapEncoderOptions options)
		{
			return options is null ? null : new[] { options };
		}
	}
}
