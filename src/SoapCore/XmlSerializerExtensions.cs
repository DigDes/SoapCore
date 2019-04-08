using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	/// <summary>Extensions to <see cref="XmlSerializer"/>.</summary>
	public static class XmlSerializerExtensions
	{
		public static T[] DeserializeArray<T>(this XmlSerializer serializer, string localname, string ns, XmlReader xmlReader)
		{
			var argument = new List<T>();
			while (xmlReader.IsStartElement(localname, ns))
			{
				argument.Add((T)serializer.Deserialize(xmlReader));
			}

			return argument.ToArray();
		}
	}
}
