using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SoapCore
{
	public static class CachedXmlSerializer
	{
		private static readonly ConcurrentDictionary<string, XmlSerializer> CachedSerializers = new ConcurrentDictionary<string, XmlSerializer>();

		public static XmlSerializer GetXmlSerializer(Type elementType, string parameterName, string parameterNs)
		{
			var key = $"{elementType}|{parameterName}|{parameterNs}";
			return CachedSerializers.GetOrAdd(key, _ => new XmlSerializer(elementType, null, new Type[0], new XmlRootAttribute(parameterName), parameterNs));
		}
	}
}
