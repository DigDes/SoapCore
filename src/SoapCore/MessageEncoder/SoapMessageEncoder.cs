// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http.Headers;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SoapCore.MessageEncoder
{
	public class SoapMessageEncoder
	{
		internal const string Soap11MediaType = "text/xml";
		internal const string Soap12MediaType = "application/soap+xml";
		private const string XmlMediaType = "application/xml";

		private readonly Encoding _writeEncoding;
		private readonly bool _optimizeWriteForUtf8;
		private readonly bool _omitXmlDeclaration;
		private readonly bool _indentXml;
		private readonly bool _supportXmlDictionaryReader;

		public SoapMessageEncoder(MessageVersion version, Encoding writeEncoding, XmlDictionaryReaderQuotas quotas, bool omitXmlDeclaration, bool indentXml)
		{
			_indentXml = indentXml;
			_omitXmlDeclaration = omitXmlDeclaration;
			if (writeEncoding == null)
			{
				throw new ArgumentNullException(nameof(writeEncoding));
			}

			_supportXmlDictionaryReader = SoapMessageEncoderDefaults.TryValidateEncoding(writeEncoding, out _);

			_writeEncoding = writeEncoding;
			_optimizeWriteForUtf8 = IsUtf8Encoding(writeEncoding);

			MessageVersion = version ?? throw new ArgumentNullException(nameof(version));

			ReaderQuotas = new XmlDictionaryReaderQuotas();
			(quotas ?? XmlDictionaryReaderQuotas.Max).CopyTo(ReaderQuotas);

			MediaType = GetMediaType(version);
			CharSet = SoapMessageEncoderDefaults.EncodingToCharSet(writeEncoding);
			ContentType = GetContentType(MediaType, CharSet);
		}

		public string ContentType { get; }

		public string MediaType { get; }

		public string CharSet { get; }

		public MessageVersion MessageVersion { get; }

		public XmlDictionaryReaderQuotas ReaderQuotas { get; }

		public bool IsContentTypeSupported(string contentType)
		{
			if (contentType == null)
			{
				throw new ArgumentNullException(nameof(contentType));
			}

			if (IsContentTypeSupported(contentType, ContentType, MediaType))
			{
				return true;
			}

			// we support a few extra content types for "none"
			if (MessageVersion.Equals(MessageVersion.None))
			{
				const string rss1MediaType = "text/xml";
				const string rss2MediaType = "application/rss+xml";
				const string atomMediaType = "application/atom+xml";
				const string htmlMediaType = "text/html";

				if (IsContentTypeSupported(contentType, rss1MediaType, rss1MediaType))
				{
					return true;
				}

				if (IsContentTypeSupported(contentType, rss2MediaType, rss2MediaType))
				{
					return true;
				}

				if (IsContentTypeSupported(contentType, htmlMediaType, atomMediaType))
				{
					return true;
				}

				if (IsContentTypeSupported(contentType, atomMediaType, atomMediaType))
				{
					return true;
				}
			}

			return false;
		}

		public async Task<Message> ReadMessageAsync(PipeReader pipeReader, int maxSizeOfHeaders, string contentType)
		{
			if (pipeReader == null)
			{
				throw new ArgumentNullException(nameof(pipeReader));
			}

			var stream = new PipeStream(pipeReader, false);
			return await ReadMessageAsync(stream, maxSizeOfHeaders, contentType);
		}

		public Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			XmlReader reader = _supportXmlDictionaryReader ?
			 	XmlDictionaryReader.CreateTextReader(stream, _writeEncoding, ReaderQuotas, dictionaryReader => { }) :
				XmlReader.Create(stream, new XmlReaderSettings());

			Message message = Message.CreateMessage(reader, maxSizeOfHeaders, MessageVersion);

			return Task.FromResult(message);
		}

		public virtual async Task WriteMessageAsync(Message message, PipeWriter pipeWriter)
		{
			if (message == null)
			{
				throw new ArgumentNullException(nameof(message));
			}

			if (pipeWriter == null)
			{
				throw new ArgumentNullException(nameof(pipeWriter));
			}

			ThrowIfMismatchedMessageVersion(message);

			using var bufferTextWriter = new BufferTextWriter(pipeWriter, _writeEncoding);
			using var xmlTextWriter = XmlWriter.Create(bufferTextWriter, new XmlWriterSettings
			{
				OmitXmlDeclaration = _optimizeWriteForUtf8 && _omitXmlDeclaration, //can only omit if utf-8
				Indent = _indentXml,
				Encoding = _writeEncoding
			});
			var xmlWriter = XmlDictionaryWriter.CreateDictionaryWriter(xmlTextWriter);
			WriteXmlCore(message, xmlWriter);

			await pipeWriter.FlushAsync();
		}

		public virtual Task WriteMessageAsync(Message message, Stream stream)
		{
			if (message == null)
			{
				throw new ArgumentNullException(nameof(message));
			}

			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			ThrowIfMismatchedMessageVersion(message);

			using var xmlTextWriter = XmlWriter.Create(stream, new XmlWriterSettings
			{
				OmitXmlDeclaration = _optimizeWriteForUtf8 && _omitXmlDeclaration, //can only omit if utf-8,
				Indent = _indentXml,
				Encoding = _writeEncoding,
				CloseOutput = false
			});
			var xmlWriter = XmlDictionaryWriter.CreateDictionaryWriter(xmlTextWriter);

			WriteXmlCore(message, xmlWriter);

			return Task.CompletedTask;
		}

		internal static string GetMediaType(MessageVersion version)
		{
			string mediaType;

			if (version.Envelope == EnvelopeVersion.Soap12)
			{
				mediaType = Soap12MediaType;
			}
			else if (version.Envelope == EnvelopeVersion.Soap11)
			{
				mediaType = Soap11MediaType;
			}
			else if (version.Envelope == EnvelopeVersion.None)
			{
				mediaType = XmlMediaType;
			}
			else
			{
				throw new InvalidOperationException($"Envelope Version '{version.Envelope}' is not supported.");
			}

			return mediaType;
		}

		internal static string GetContentType(string mediaType, string charSet)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}; charset={1}", mediaType, charSet);
		}

		internal bool IsContentTypeSupported(string contentType, string supportedContentType, string supportedMediaType)
		{
			if (supportedContentType == contentType)
			{
				return true;
			}

			if (contentType.Length > supportedContentType.Length &&
				contentType.StartsWith(supportedContentType, StringComparison.Ordinal) &&
				contentType[supportedContentType.Length] == ';')
			{
				return true;
			}

			// now check case-insensitively
			if (contentType.StartsWith(supportedContentType, StringComparison.OrdinalIgnoreCase))
			{
				if (contentType.Length == supportedContentType.Length)
				{
					return true;
				}
				else if (contentType.Length > supportedContentType.Length)
				{
					char ch = contentType[supportedContentType.Length];

					// Linear Whitespace is allowed to appear between the end of one property and the semicolon.
					// LWS = [CRLF]? (SP | HT)+
					if (ch == ';')
					{
						return true;
					}

					// Consume the [CRLF]?
					int i = supportedContentType.Length;
					if (ch == '\r' && contentType.Length > supportedContentType.Length + 1 && contentType[i + 1] == '\n')
					{
						i += 2;
						ch = contentType[i];
					}

					// Look for a ';' or nothing after (SP | HT)+
					if (ch == ' ' || ch == '\t')
					{
						i++;
						while (i < contentType.Length)
						{
							ch = contentType[i];
							if (ch != ' ' && ch != '\t')
							{
								break;
							}

							++i;
						}
					}

					if (ch == ';' || i == contentType.Length)
					{
						return true;
					}
				}
			}

			// sometimes we get a contentType that has parameters, but our encoders
			// merely expose the base content-type, so we will check a stripped version
			try
			{
				MediaTypeHeaderValue parsedContentType = MediaTypeHeaderValue.Parse(contentType);

				if (supportedMediaType.Length > 0 && !supportedMediaType.Equals(parsedContentType.MediaType, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}

				if (!IsCharSetSupported(parsedContentType.CharSet))
				{
					return false;
				}
			}
			catch (FormatException)
			{
				// bad content type, so we definitely don't support it!
				return false;
			}

			return true;
		}

		internal virtual bool IsCharSetSupported(string charset)
		{
			return CharSet?.Equals(charset, StringComparison.OrdinalIgnoreCase)
			       ?? false;
		}

		private static bool IsUtf8Encoding(Encoding encoding)
		{
			return encoding.WebName == "utf-8";
		}

		private void ThrowIfMismatchedMessageVersion(Message message)
		{
			if (!message.Version.Equals(MessageVersion))
			{
				throw new InvalidOperationException($"Message version {message.Version.Envelope} doesn't match encoder version {message.Version.Envelope}");
			}
		}

		private void WriteXmlCore(Message message, XmlWriter xmlWriter)
		{
			message.WriteMessage(xmlWriter);
			xmlWriter.WriteEndDocument();
			xmlWriter.Flush();
			xmlWriter.Dispose();
		}
	}
}
