// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace SoapCore.MessageEncoder
{
	internal class SoapMessageEncoderDefaults
	{
		public static readonly Encoding[] SupportedEncodings = { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode };

		// Desktop: System.ServiceModel.Configuration.ConfigurationStrings.Soap12WSAddressing10;
		public static readonly CharSetEncoding[] CharSetEncodings =
		{
			new CharSetEncoding("utf-8", Encoding.UTF8),
			new CharSetEncoding("utf-16LE", Encoding.Unicode),
			new CharSetEncoding("utf-16BE", Encoding.BigEndianUnicode),
			new CharSetEncoding("utf-16", null),   // Ignore.  Ambiguous charSet, so autodetect.
			new CharSetEncoding(null, null),       // CharSet omitted, so autodetect.
		};

		public static void ValidateEncoding(Encoding encoding)
		{
			if (TryValidateEncoding(encoding, out var error) == false)
			{
				throw error;
			}
		}

		public static bool TryValidateEncoding(Encoding encoding, out Exception exception)
		{
			string charSet = encoding.WebName;
			Encoding[] supportedEncodings = SupportedEncodings;

			for (int i = 0; i < supportedEncodings.Length; i++)
			{
				if (charSet == supportedEncodings[i].WebName)
				{
					exception = null;
					return true;
				}
			}

			exception = new ArgumentException($"The text encoding '{charSet}' used in the text message format is not supported.", nameof(encoding));
			return false;
		}

		public static string EncodingToCharSet(Encoding encoding)
		{
			string webName = encoding.WebName;

			foreach (var charSetEncoding in CharSetEncodings)
			{
				if (charSetEncoding.Encoding == null)
				{
					continue;
				}

				if (charSetEncoding.Encoding.WebName == webName)
				{
					return charSetEncoding.CharSet;
				}
			}

			return null;
		}

		public class CharSetEncoding
		{
			public CharSetEncoding(string charSet, Encoding enc)
			{
				CharSet = charSet;
				Encoding = enc;
			}

			public string CharSet { get; }
			public Encoding Encoding { get; }
		}
	}
}
