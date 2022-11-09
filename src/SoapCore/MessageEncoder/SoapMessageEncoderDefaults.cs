// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http.Headers;
using System.Text;

namespace SoapCore.MessageEncoder
{
	internal class SoapMessageEncoderDefaults
	{
		public const int MaxSoapHeaderSizeDefault = 0x10000;
		private static readonly Encoding[] XmlDictionaryReaderSupportedEncodings = { DefaultEncodings.UTF8, DefaultEncodings.Unicode, DefaultEncodings.BigEndianUnicode };

		// Desktop: System.ServiceModel.Configuration.ConfigurationStrings.Soap12WSAddressing10;
		private static readonly CharSetEncoding[] CharSetEncodings =
		{
			new CharSetEncoding("utf-8", DefaultEncodings.UTF8),
			new CharSetEncoding("utf-16LE", DefaultEncodings.Unicode),
			new CharSetEncoding("utf-16BE", DefaultEncodings.BigEndianUnicode),
			new CharSetEncoding("utf-16", null),   // Ignore.  Ambiguous charSet, so autodetect.
			new CharSetEncoding(null, null),       // CharSet omitted, so autodetect.
			new CharSetEncoding("ISO8859-1", DefaultEncodings.Iso88591),
			new CharSetEncoding("windows-1252", DefaultEncodings.Iso88591) // technically it's same encodings, but in .NET Core windows-* encodings are not supported out of the box
		};

		public static bool TryValidateEncoding(Encoding encoding, out Exception exception)
		{
			string charSet = encoding.WebName;
			Encoding[] supportedEncodings = XmlDictionaryReaderSupportedEncodings;

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

		public static Encoding ContentTypeToEncoding(string contentType)
		{
			var charSet = MediaTypeHeaderValue.Parse(contentType).CharSet;

			foreach (var charSetEncoding in CharSetEncodings)
			{
				if (charSetEncoding.Encoding == null)
				{
					continue;
				}

				if (charSetEncoding.CharSet == charSet)
				{
					return charSetEncoding.Encoding;
				}
			}

			return null;
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
