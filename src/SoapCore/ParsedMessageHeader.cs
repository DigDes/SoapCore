using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Linq;

namespace SoapCore
{
	public class ParsedMessageHeader : MessageHeader
	{
		private readonly XElement[] _content;
		private readonly XAttribute[] _attributes;
		private readonly string _value;

		public ParsedMessageHeader(string name, string ns, XElement[] content, string value, XAttribute[] attributes)
		{
			Name = name;
			Namespace = ns;
			_content = content;
			_attributes = attributes;
			_value = value;
		}

		public override string Name { get; }
		public override string Namespace { get; }

		protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
		{
			// Write custom attributes
			foreach (var attr in _attributes)
			{
				// If the attribute is an xmlns declaration and its value is different
				// from the header's namespace, skip it to avoid conflicts.

				// This should not really be necessary, but it is a safeguard against malformed
				// SOAP messages that might include redundant or conflicting
				// namespace declarations in the header
				if (attr.Name.LocalName == "xmlns" && attr.Value != Namespace)
				{
					continue;
				}

				writer.WriteAttributeString(attr.Name.LocalName, attr.Value);
			}

			if (_content != null && _content.Length > 0)
			{
				foreach (var content in _content)
				{
					// Write the XML content inside the header
					content.WriteTo(writer);
				}
			}
			else
			{
				writer.WriteString(_value);
			}
		}
	}
}
