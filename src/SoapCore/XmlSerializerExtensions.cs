using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	/// <summary>Extensions to <see cref="XmlSerializer"/>.</summary>
	public static class XmlSerializerExtensions
	{
		/// <summary>
		/// Deserializes the XML document contained by the specified <see cref="XmlReader"/>.
		/// </summary>
		/// <typeparam name="T"> The type of the object that this <see cref="XmlSerializer"/> can serialize.</typeparam>
		/// <param name="serializer">The <see cref="XmlSerializer"/>.</param>
		/// <param name="localname">The string to match against the LocalName property of the element found.</param>
		/// <param name="ns">The string to match against the NamespaceURI property of the element found.</param>
		/// <param name="xmlReader">The System.xml.XmlReader that contains the XML document to deserialize.</param>
		/// <returns>The objects being deserialized.</returns>
		/// <exception cref="InvalidOperationException">
		/// An error occurred during deserialization. The original exception is available
		/// using the <see cref="Exception.InnerException"/> property.
		/// </exception>
		public static T[] DeserializeArray<T>(this XmlSerializer serializer, string localname, string ns, XmlReader xmlReader)
		{
			var argument = new List<T>();
			while (xmlReader.IsStartElement(localname, ns))
			{
				argument.Add((T)serializer.Deserialize(xmlReader));
			}

			if (argument.Count == 0 && xmlReader.HasValue)
			{
				//If there was no valid array items we can assume that any value is trash and skip it
				xmlReader.Skip();
			}

			return argument.ToArray();
		}

		/// <summary>
		/// Serializes the specified objects and writes the XML document to a file using the specified <see cref="XmlWriter"/>.
		/// </summary>
		/// <param name="serializer">The <see cref="XmlSerializer"/>.</param>
		/// <param name="xmlWriter">The <see cref="XmlWriter"/> used to write the XML document.</param>
		/// <param name="os">The objects to serialize.</param>
		/// <exception cref="InvalidOperationException">
		/// An error occurred during serialization. The original exception is available using
		/// the <see cref="Exception.InnerException"/> property.
		/// </exception>
		public static void SerializeArray(this XmlSerializer serializer, XmlWriter xmlWriter, object[] os)
		{
			foreach (var o in os)
			{
				serializer.Serialize(xmlWriter, o);
			}
		}
	}
}
