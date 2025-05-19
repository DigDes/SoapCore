using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace SoapCore
{

	public static class CachedDataContractSerializer
	{
		private static readonly ConcurrentDictionary<string, DataContractSerializer> CachedSerializers = new ConcurrentDictionary<string, DataContractSerializer>();

		public static DataContractSerializer GetDataContractSerializer(Type elementType, string parameterName, string parameterNs, IEnumerable<Type> knownTypes = null)
		{
			var key = $"{elementType}|{parameterName}|{parameterNs}";

			if (knownTypes != null)
			{
				return CachedSerializers.GetOrAdd(key, _ => new DataContractSerializer(elementType, parameterName, parameterNs, knownTypes));
			}
			else
			{
				return CachedSerializers.GetOrAdd(key, _ => new DataContractSerializer(elementType, parameterName, parameterNs));
			}
		}

		public static DataContractSerializer GetDataContractSerializer(Type elementType, IEnumerable<Type> knownTypes = null)
		{
			var key = $"{elementType}|_|_";

			if (knownTypes != null)
			{
				return CachedSerializers.GetOrAdd(key, _ => new DataContractSerializer(elementType, knownTypes));
			}
			else
			{
				return CachedSerializers.GetOrAdd(key, _ => new DataContractSerializer(elementType));
			}
		}
	}
}
