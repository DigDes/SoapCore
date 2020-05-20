using System;
using System.Collections.Generic;
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
	}
}
