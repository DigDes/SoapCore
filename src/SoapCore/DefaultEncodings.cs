using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore
{
	internal class DefaultEncodings
	{
		public static Encoding UTF8 => new UTF8Encoding(false);
		public static Encoding Unicode => Encoding.Unicode;
		public static Encoding BigEndianUnicode => Encoding.BigEndianUnicode;
	}
}
