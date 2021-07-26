using System;
using System.Collections.Concurrent;
using System.Xml.Serialization;

namespace SoapCore
{
	public static class CachedXmlSerializer
	{
		private static readonly ConcurrentDictionary<string, XmlSerializer> CachedSerializers = new ConcurrentDictionary<string, XmlSerializer>();

		public static XmlSerializer GetXmlSerializer(Type elementType, string parameterName, string parameterNs)
		{
			var key = $"{elementType}|{parameterName}|{parameterNs}";
			return CachedSerializers.GetOrAdd(key, _ => new XmlSerializer(elementType, null, Array.Empty<Type>(), new XmlRootAttribute(parameterName), parameterNs));
		}
	}
}
