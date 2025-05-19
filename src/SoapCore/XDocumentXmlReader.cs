using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SoapCore
{
	public class XDocumentXmlReader : XmlReader
	{
		private readonly XmlReader _reader;
		private MemoryStream _binaryStream;
		private bool _isReadingBinary;

		public XDocumentXmlReader(XDocument document)
		{
			_reader = document.CreateReader();
		}

		public override XmlNodeType NodeType => _reader.NodeType;
		public override string Name => _reader.Name;
		public override string LocalName => _reader.LocalName;
		public override string NamespaceURI => _reader.NamespaceURI;
		public override string Prefix => _reader.Prefix;
		public override bool HasValue => _reader.HasValue;
		public override string Value => _reader.Value;
		public override int Depth => _reader.Depth;
		public override string BaseURI => _reader.BaseURI;
		public override bool IsEmptyElement => _reader.IsEmptyElement;
		public override int AttributeCount => _reader.AttributeCount;
		public override bool EOF => _reader.EOF;
		public override ReadState ReadState => _reader.ReadState;
		public override XmlNameTable NameTable => _reader.NameTable;

		public override int ReadContentAsBase64(byte[] buffer, int index, int count)
		{
			return ReadBinary(buffer, index, count, true);
		}

		public override int ReadElementContentAsBase64(byte[] buffer, int index, int count)
		{
			if (!_isReadingBinary)
			{
				if (!Read() || (_reader.NodeType != XmlNodeType.Text && _reader.NodeType != XmlNodeType.Whitespace))
				{
					return 0;
				}
			}

			return ReadContentAsBase64(buffer, index, count);
		}

		public override int ReadContentAsBinHex(byte[] buffer, int index, int count)
		{
			return ReadBinary(buffer, index, count, false);
		}

		public override int ReadElementContentAsBinHex(byte[] buffer, int index, int count)
		{
			if (!_isReadingBinary)
			{
				if (!Read() || (_reader.NodeType != XmlNodeType.Text && _reader.NodeType != XmlNodeType.Whitespace))
				{
					return 0;
				}
			}

			return ReadContentAsBinHex(buffer, index, count);
		}

		public int ReadBinary(byte[] buffer, int index, int count, bool isBase64)
		{
			EnsureBinaryStream(isBase64);

			var result = _binaryStream!.Read(buffer, index, count);
			if (_binaryStream.Position == _binaryStream.Length)
			{
				_isReadingBinary = false;
			}

			return result;
		}

		public override bool Read()
		{
			_isReadingBinary = false;
			_binaryStream?.Dispose();
			_binaryStream = null;
			return _reader.Read();
		}

		public override string GetAttribute(int i) => _reader.GetAttribute(i);
		public override string GetAttribute(string name) => _reader.GetAttribute(name);
		public override string GetAttribute(string name, string namespaceURI) => _reader.GetAttribute(name, namespaceURI);
		public override bool MoveToAttribute(string name) => _reader.MoveToAttribute(name);
		public override bool MoveToAttribute(string name, string ns) => _reader.MoveToAttribute(name, ns);
		public override bool MoveToFirstAttribute() => _reader.MoveToFirstAttribute();
		public override bool MoveToNextAttribute() => _reader.MoveToNextAttribute();
		public override bool MoveToElement() => _reader.MoveToElement();
		public override void Close() => _reader.Close();
		public override string LookupNamespace(string prefix) => _reader.LookupNamespace(prefix);
		public override void ResolveEntity() => _reader.ResolveEntity();
		public override bool ReadAttributeValue() => _reader.ReadAttributeValue();

		protected override void Dispose(bool disposing)
		{
			_binaryStream?.Dispose();
			_binaryStream = null;
			base.Dispose(disposing);
		}

#if NET8_0_OR_GREATER
		private static byte[] ConvertHexStringToBytes(string hexString) => Convert.FromHexString(hexString);
#else
		private static byte[] ConvertHexStringToBytes(string hexString)
		{
			return Enumerable.Range(0, hexString.Length)
							 .Where(x => x % 2 == 0)
							 .Select(x => Convert.ToByte(hexString.Substring(x, 2), 16))
							 .ToArray();
		}
#endif

		private void EnsureBinaryStream(bool isBase64)
		{
			if (!_isReadingBinary)
			{
				string data = ReadAllTextContent();
				byte[] binaryData = isBase64 ? Convert.FromBase64String(data) : ConvertHexStringToBytes(data);

				_binaryStream = new MemoryStream(binaryData);
				_isReadingBinary = true;
			}
		}

		private string ReadAllTextContent()
		{
			var sb = new StringBuilder();

			while (_reader.NodeType == XmlNodeType.Text || _reader.NodeType == XmlNodeType.Whitespace)
			{
				sb.Append(_reader.Value);
				_reader.Read();
			}

			return sb.ToString();
		}
	}
}
