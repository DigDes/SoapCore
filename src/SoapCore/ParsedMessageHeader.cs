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
