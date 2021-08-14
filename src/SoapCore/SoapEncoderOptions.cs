using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace SoapCore
{
	public class SoapEncoderOptions
	{
		public MessageVersion MessageVersion { get; set; }
		public Encoding WriteEncoding { get; set; }
		public XmlDictionaryReaderQuotas ReaderQuotas { get; set; }

		internal static SoapEncoderOptions[] Default()
		{
			return new SoapEncoderOptions[]
			{
				new SoapEncoderOptions
				{
					MessageVersion = MessageVersion.Soap11,
					WriteEncoding = Encoding.UTF8,
					ReaderQuotas = XmlDictionaryReaderQuotas.Max
				}
			};
		}

		internal static SoapEncoderOptions[] ToArray(SoapEncoderOptions options)
		{
			return options is null ? null : new[] { options };
		}
	}
}
