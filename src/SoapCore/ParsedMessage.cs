using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace SoapCore
{
	public class ParsedMessage : Message
	{
		private readonly MessageHeaders _headers;
		private readonly MessageProperties _properties;
		private readonly MessageVersion _version;
		private readonly XDocument _body;
		private readonly bool _isEmpty;

		public ParsedMessage(MessageHeaders headers, MessageProperties properties, MessageVersion version, XDocument body, bool isEmpty)
		{
			_headers = headers;
			_properties = properties;
			_version = version;
			_body = body;
			_isEmpty = isEmpty;
		}

		public override MessageHeaders Headers => _headers;
		public override MessageProperties Properties => _properties;
		public override MessageVersion Version => _version;

		public override bool IsEmpty => _isEmpty;

		public static async Task<ParsedMessage> FromStreamAsync(Stream stream, Encoding readEncoding, MessageVersion version, CancellationToken ct)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			if (version == null)
			{
				throw new ArgumentNullException(nameof(version));
			}

			var pipe = new Pipe();

			var reader = new StreamReader(pipe.Reader.AsStream(), readEncoding);

#if NETCOREAPP3_0_OR_GREATER
			var envelopeTask = XDocument.LoadAsync(reader, LoadOptions.None, ct);
			await stream.CopyToAsync(pipe.Writer.AsStream(), ct);
#else
			var envelopeTask = Task.Factory.StartNew(() => { return XDocument.Load(reader); }, ct);

			byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
			try
			{
				var writeStream = pipe.Writer.AsStream();
				while (true)
				{
					int readBytes = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
					if (readBytes == 0)
					{
						break;
					}

					await writeStream.WriteAsync(buffer, 0, readBytes, ct);
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
#endif
			await pipe.Writer.CompleteAsync();

			var envelope = await envelopeTask;

			var headers = ExtractSoapHeaders(envelope, version);

			//var properties = ExtractSoapProperties(httpRequest);
			(var body, var isEmpty) = ExtractSoapBody(envelope, version);

			return new ParsedMessage(headers, new MessageProperties(), version, body, isEmpty);
		}

		public XDocument GetBodyAsXDocument()
		{
			return _body;
		}

		public override string ToString()
		{
			return _body?.ToString() ?? "<Empty Body>";
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			ResetMessageConsumed();

			using (var reader = InternalGetReaderAtBodyContents())
			{
				writer.WriteNode(reader, true);
			}
		}

		protected override XmlDictionaryReader OnGetReaderAtBodyContents()
		{
			ResetMessageConsumed();

			return InternalGetReaderAtBodyContents();
		}

		private XmlDictionaryReader InternalGetReaderAtBodyContents()
		{
			var reader = new XDocumentXmlReader(_body);

			XNamespace soapNs = _version.Envelope.Namespace();

			reader.ReadToFollowing("Body", soapNs.ToString());

			while (reader.Read() && reader.NodeType != XmlNodeType.Element && reader.NodeType != XmlNodeType.EndElement)
			{
			}

			return XmlDictionaryReader.CreateDictionaryReader(reader);
		}

		protected override void OnClose()
		{
			_properties.Dispose();
			base.OnClose();
		}

		protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
		{
			return new ParsedMessageBuffer(this);
		}

		/// <summary>
		/// Extracts SOAP headers from the SOAP message body.
		/// </summary>
		private static MessageHeaders ExtractSoapHeaders(XDocument envelope, MessageVersion version)
		{
			var headers = new MessageHeaders(version);
			var root = envelope.Root;
			if (root == null)
			{
				return headers;
			}

			XNamespace soapNs = version.Envelope.Namespace();
			var headerNode = root.Element(soapNs + "Header");
			if (headerNode != null)
			{
				foreach (var element in headerNode.Elements())
				{
					var header = new ParsedMessageHeader(element.Name.LocalName, element.Name.NamespaceName, element.Elements().ToArray(), element.Value, element.Attributes().ToArray());
					headers.Add(header);
				}
			}

			return headers;
		}

		/// <summary>
		/// Extracts only the SOAP Body content.
		/// </summary>
		private static (XDocument, bool isEmpty) ExtractSoapBody(XDocument envelope, MessageVersion version)
		{
			var root = envelope.Root;
			if (root == null)
			{
				return (new XDocument(), true);
			}

			XNamespace soapNs = version.Envelope.Namespace();
			var bodyNode = root.Element(soapNs + "Body");
			if (bodyNode == null)
			{
				return (new XDocument(), true);
			}

			//return new XDocument(bodyNode.Elements().FirstOrDefault());
			return (new XDocument(bodyNode), bodyNode.IsEmpty);
		}

		/// <summary>
		/// Extracts SOAP-specific properties.
		/// </summary>
		private static MessageProperties ExtractSoapProperties(HttpRequest httpRequest)
		{
			var properties = new MessageProperties
			{
				["HttpMethod"] = httpRequest.Method,
				["RequestUri"] = httpRequest.Path + httpRequest.QueryString
			};

			if (httpRequest.ContentType != null)
			{
				properties["Content-Type"] = httpRequest.ContentType;
			}

			return properties;
		}

		private void ResetMessageConsumed()
		{
			var stateField = typeof(Message).GetProperty("State");
			if (stateField != null)
			{
				// Set the state to 'Created' to allow reuse
				stateField.SetValue(this, MessageState.Created);
			}
			else
			{
				throw new InvalidOperationException("Unable to access the internal state field.");
			}
		}
	}
}
