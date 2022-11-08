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
using Microsoft.AspNetCore.Http;

namespace SoapCore.MessageEncoder
{
	public class SoapMessageEncoder
	{
		internal const string Soap11MediaType = "text/xml";
		internal const string Soap12MediaType = "application/soap+xml";
		private const string XmlMediaType = "application/xml";

		private readonly Encoding _writeEncoding;
		private readonly bool _overwriteResponseContentType;
		private readonly bool _optimizeWriteForUtf8;
		private readonly bool _omitXmlDeclaration;
		private readonly bool _indentXml;
		private readonly bool _checkXmlCharacters;

		public SoapMessageEncoder(MessageVersion version, Encoding writeEncoding, bool overwriteResponseContentType, XmlDictionaryReaderQuotas quotas, bool omitXmlDeclaration, bool indentXml, bool checkXmlCharacters, XmlNamespaceManager xmlNamespaceOverrides, string bindingName, string portName, int maxSoapHeaderSize = SoapMessageEncoderDefaults.MaxSoapHeaderSizeDefault)
		{
			_indentXml = indentXml;
			_omitXmlDeclaration = omitXmlDeclaration;
			_checkXmlCharacters = checkXmlCharacters;
			BindingName = bindingName;
			PortName = portName;

			_writeEncoding = writeEncoding;
			_optimizeWriteForUtf8 = IsUtf8Encoding(writeEncoding);

			_overwriteResponseContentType = overwriteResponseContentType;

			MessageVersion = version ?? throw new ArgumentNullException(nameof(version));

			ReaderQuotas = new XmlDictionaryReaderQuotas();
			(quotas ?? XmlDictionaryReaderQuotas.Max).CopyTo(ReaderQuotas);
			MaxSoapHeaderSize = maxSoapHeaderSize;

			MediaType = GetMediaType(version);
			CharSet = SoapMessageEncoderDefaults.EncodingToCharSet(writeEncoding);
			ContentType = GetContentType(MediaType, CharSet);

			XmlNamespaceOverrides = xmlNamespaceOverrides;
		}

		public string BindingName { get; }
		public string PortName { get; }

		public string ContentType { get; }

		public string MediaType { get; }

		public string CharSet { get; }

		public MessageVersion MessageVersion { get; }

		public XmlDictionaryReaderQuotas ReaderQuotas { get; }

		public int MaxSoapHeaderSize { get; }

		public XmlNamespaceManager XmlNamespaceOverrides { get; }

		public bool IsContentTypeSupported(string contentType, bool checkCharset)
		{
			if (contentType == null)
			{
				throw new ArgumentNullException(nameof(contentType));
			}

			if (IsContentTypeSupported(contentType, ContentType, MediaType, checkCharset))
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

				if (IsContentTypeSupported(contentType, rss1MediaType, rss1MediaType, checkCharset))
				{
					return true;
				}

				if (IsContentTypeSupported(contentType, rss2MediaType, rss2MediaType, checkCharset))
				{
					return true;
				}

				if (IsContentTypeSupported(contentType, htmlMediaType, atomMediaType, checkCharset))
				{
					return true;
				}

				if (IsContentTypeSupported(contentType, atomMediaType, atomMediaType, checkCharset))
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

			using var stream = pipeReader.AsStream(true);
			return await ReadMessageAsync(stream, maxSizeOfHeaders, contentType);
		}

		public Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			XmlReader reader;

			var readEncoding = SoapMessageEncoderDefaults.ContentTypeToEncoding(contentType);

			if (readEncoding == null)
			{
				// Fallback to default or writeEncoding
				readEncoding = _writeEncoding;
			}

			var supportXmlDictionaryReader = SoapMessageEncoderDefaults.TryValidateEncoding(readEncoding, out _);

			if (supportXmlDictionaryReader)
			{
				reader = XmlDictionaryReader.CreateTextReader(stream, readEncoding, ReaderQuotas, dictionaryReader => { });
			}
			else
			{
				var streamReaderWithEncoding = new StreamReader(stream, readEncoding);
				var xmlReaderSettings = new XmlReaderSettings() { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit, CloseInput = true };
				reader = XmlReader.Create(streamReaderWithEncoding, xmlReaderSettings);
			}

			Message message = Message.CreateMessage(reader, maxSizeOfHeaders, MessageVersion);

			return Task.FromResult(message);
		}

		public virtual async Task WriteMessageAsync(Message message, HttpContext httpContext, PipeWriter pipeWriter)
		{
			if (message == null)
			{
				throw new ArgumentNullException(nameof(message));
			}

			if (httpContext == null)
			{
				throw new ArgumentNullException(nameof(httpContext));
			}

			if (pipeWriter == null)
			{
				throw new ArgumentNullException(nameof(pipeWriter));
			}

			ThrowIfMismatchedMessageVersion(message);

			//Custom string writer with custom encoding support
			using (var stringWriter = new CustomStringWriter(_writeEncoding))
			{
				using (var xmlTextWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
				{
					OmitXmlDeclaration = _optimizeWriteForUtf8 && _omitXmlDeclaration, //can only omit if utf-8
					Indent = _indentXml,
					Encoding = _writeEncoding,
					CloseOutput = true,
					CheckCharacters = _checkXmlCharacters
				}))
				{
					using var xmlWriter = XmlDictionaryWriter.CreateDictionaryWriter(xmlTextWriter);
					message.WriteMessage(xmlWriter);
					xmlWriter.WriteEndDocument();
					xmlWriter.Flush();
				}

				var data = stringWriter.ToString();
				var soapMessage = _writeEncoding.GetBytes(data);

				//Set Content-length in Response
				httpContext.Response.ContentLength = soapMessage.Length;

				if (_overwriteResponseContentType)
				{
					httpContext.Response.ContentType = ContentType;
				}

				await pipeWriter.WriteAsync(soapMessage);
				await pipeWriter.FlushAsync();
			}
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
				CloseOutput = false,
				CheckCharacters = _checkXmlCharacters
			});

			using var xmlWriter = XmlDictionaryWriter.CreateDictionaryWriter(xmlTextWriter);
			message.WriteMessage(xmlWriter);
			xmlWriter.WriteEndDocument();
			xmlWriter.Flush();

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

		internal bool IsContentTypeSupported(string contentType, string supportedContentType, string supportedMediaType, bool checkCharset)
		{
			if (supportedContentType == contentType)
			{
				return true;
			}

			MediaTypeHeaderValue parsedContentType;

			try
			{
				parsedContentType = MediaTypeHeaderValue.Parse(contentType);
			}
			catch (FormatException)
			{
				//bad format
				return false;
			}

			if (parsedContentType.MediaType.Equals(MediaType, StringComparison.OrdinalIgnoreCase))
			{
				if (!checkCharset || string.IsNullOrWhiteSpace(parsedContentType.CharSet) || parsedContentType.CharSet.Equals(CharSet, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// sometimes we get a contentType that has parameters, but our encoders
			// merely expose the base content-type, so we will check a stripped version
			if (supportedMediaType.Length > 0 && !supportedMediaType.Equals(parsedContentType.MediaType, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (!IsCharSetSupported(parsedContentType.CharSet))
			{
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
	}
}
