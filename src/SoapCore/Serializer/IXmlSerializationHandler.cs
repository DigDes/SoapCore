using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace SoapCore.Serializer
{
	public delegate IXmlSerializationHandler IXmlSerializationHandlerResolver(Type identifier);

	public interface IXmlSerializationHandler
	{
		object DeserializeInputParameter(XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, ICustomAttributeProvider customAttributeProvider, IEnumerable<Type> knownTypes = null);
	}
}
