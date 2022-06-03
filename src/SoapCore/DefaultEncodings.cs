using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore
{
	internal class DefaultEncodings
	{
		public static Encoding UTF8 => new UTF8Encoding(false);
		public static Encoding Unicode => new UnicodeEncoding(false, false);
		public static Encoding BigEndianUnicode => new UnicodeEncoding(true, false);
	}
}
