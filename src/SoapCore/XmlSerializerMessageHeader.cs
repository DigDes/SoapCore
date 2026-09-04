using System;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	public class XmlSerializerMessageHeader : MessageHeader
	{
		private const string Xmlns = "xmlns";
		private readonly object _value;
		private readonly Type _valueType;
		private readonly bool _mustUnderstand;

		public XmlSerializerMessageHeader(string name, string ns, object value, Type valueType, bool mustUnderstand)
		{
			Name = name;
			Namespace = ns;
			_value = value;
			_valueType = valueType;
			_mustUnderstand = mustUnderstand;
		}

		public override string Name { get; }
		public override string Namespace { get; }
		public override bool MustUnderstand => _mustUnderstand;

		protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
		{
			if (_value == null)
			{
				return;
			}

			// XmlSerializer always writes a root element, but the surrounding MessageHeader element
			// is already opened by OnWriteStartHeader. Serialize into a temporary XmlDocument so
			// only its inner content can then be copied to the actual header writer.
			var serializer = CachedXmlSerializer.GetXmlSerializer(_valueType, Name, Namespace);
			var doc = new XmlDocument();
			using (var docWriter = doc.CreateNavigator().AppendChild())
			{
				var namespaces = new XmlSerializerNamespaces();
				namespaces.Add(string.Empty, Namespace);
				serializer.Serialize(docWriter, _value, namespaces);
			}

			// Copy the root's attributes onto the header element.
			foreach (XmlAttribute attr in doc.DocumentElement.Attributes)
			{
				// xmlns="..." default-namespace declaration - skip if it matches the header's
				// own namespace (already declared by OnWriteStartHeader); otherwise emit as xmlns.
				if (attr.Prefix.Length == 0 && attr.LocalName == Xmlns)
				{
					if (attr.Value == Namespace)
					{
						continue;
					}

					writer.WriteAttributeString(Xmlns, attr.Value);
					continue;
				}

				// Other attributes (including xmlns:prefix="..." declarations XmlSerializer produced)
				// XmlDictionaryWriter recognizes the xmlns NamespaceURI and emits them as namespace declarations when needed.
				writer.WriteAttributeString(attr.Prefix, attr.LocalName, attr.NamespaceURI, attr.Value);
			}

			// Copy the root's children into the header element.
			foreach (XmlNode child in doc.DocumentElement.ChildNodes)
			{
				child.WriteTo(writer);
			}
		}
	}
}
