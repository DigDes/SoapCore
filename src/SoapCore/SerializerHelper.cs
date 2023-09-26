using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.CSharp;

namespace SoapCore
{
	internal class SerializerHelper
	{
		private readonly SoapSerializer _serializer;

		public SerializerHelper(SoapSerializer serializer)
		{
			_serializer = serializer;
		}

		public object DeserializeInputParameter(
			System.Xml.XmlDictionaryReader xmlReader,
			Type parameterType,
			string parameterName,
			string parameterNs,
			ICustomAttributeProvider customAttributeProvider,
			IEnumerable<Type> knownTypes = null)
		{
			// Advance past any whitespace.
			while (xmlReader.NodeType == System.Xml.XmlNodeType.Whitespace && xmlReader.Read())
			{
			}

			if (xmlReader.IsStartElement(parameterName, parameterNs))
			{
				xmlReader.MoveToStartElement(parameterName, parameterNs);

				if (xmlReader.IsStartElement(parameterName, parameterNs))
				{
					switch (_serializer)
					{
						case SoapSerializer.XmlSerializer:
							if (!parameterType.IsArray || parameterType.GetElementType()?.IsArray == true)
							{
								// case [XmlElement("parameter")] int parameter
								// case [XmlArray("parameter")] int[] parameter
								return DeserializeObject(xmlReader, parameterType, parameterName, parameterNs);
							}
							else
							{
								// case int[] parameter
								// case [XmlElement("parameter")] int[] parameter
								// case [XmlArray("parameter"), XmlArrayItem(ElementName = "item")] int[] parameter
								return DeserializeArrayXmlSerializer(xmlReader, parameterType, parameterName, parameterNs, customAttributeProvider);
							}

						case SoapSerializer.DataContractSerializer:
							return DeserializeDataContract(xmlReader, parameterType, parameterName, parameterNs, knownTypes);

						default:
							throw new NotImplementedException();
					}
				}
			}

			if (parameterType.IsArray)
			{
				return DeserializeArrayXmlSerializer(xmlReader, parameterType, parameterName, parameterNs, customAttributeProvider);
			}

			return null;
		}

		private static object DeserializeObject(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs)
		{
			// see https://referencesource.microsoft.com/System.Xml/System/Xml/Serialization/XmlSerializer.cs.html#c97688a6c07294d5
			var elementType = parameterType.GetElementType();

			if (elementType == null || parameterType.IsArray)
			{
				elementType = parameterType;
			}

			var serializer = CachedXmlSerializer.GetXmlSerializer(elementType, parameterName, parameterNs);

			if (elementType == typeof(Stream) || typeof(Stream).IsAssignableFrom(elementType))
			{
				xmlReader.Read();
				return new MemoryStream(xmlReader.ReadContentAsBase64(), false);
			}

			if (elementType == typeof(XmlElement) || elementType == typeof(XmlNode))
			{
				var xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(xmlReader.ReadInnerXml());
				var xmlNode = xmlDoc.FirstChild;
				return xmlNode;
			}

			return serializer.Deserialize(xmlReader);
		}

		private static object DeserializeDataContract(
			System.Xml.XmlDictionaryReader xmlReader,
			Type parameterType,
			string parameterName,
			string parameterNs,
			IEnumerable<Type> knownTypes = null)
		{
			var elementType = parameterType.GetElementType();

			if (elementType == null || parameterType.IsArray)
			{
				elementType = parameterType;
			}

			var serializer = knownTypes is null
				? new DataContractSerializer(elementType, parameterName, parameterNs)
				: new DataContractSerializer(elementType, parameterName, parameterNs, knownTypes);

			return serializer.ReadObject(xmlReader, verifyObjectName: true);
		}

		private XmlElementAttribute ChoiceElementToSerialize(System.Xml.XmlDictionaryReader xmlReader, XmlElementAttribute[] xmlElementAttributes, string parameterNs)
		{
			if (xmlElementAttributes != null && xmlElementAttributes.Length > 0)
			{
				foreach (XmlElementAttribute xmlElementAttribute in xmlElementAttributes)
				{
					if (xmlReader.IsStartElement(xmlElementAttribute.ElementName, parameterNs))
					{
						return xmlElementAttribute;
					}
				}
			}

			return null;
		}

		private object DeserializeArrayXmlSerializer(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, ICustomAttributeProvider customAttributeProvider)
		{
			var xmlArrayAttributes = customAttributeProvider.GetCustomAttributes(typeof(XmlArrayItemAttribute), true);
			XmlArrayItemAttribute xmlArrayItemAttribute = xmlArrayAttributes.FirstOrDefault() as XmlArrayItemAttribute;
			XmlElementAttribute[] xmlElementAttributes = customAttributeProvider.GetCustomAttributes(typeof(XmlElementAttribute), true) as XmlElementAttribute[];

			// Choice : if an array has a choice of item, the first one in the XML is the only considered to fill the array.
			XmlElementAttribute xmlElementAttribute = ChoiceElementToSerialize(xmlReader, xmlElementAttributes, parameterNs) ?? xmlElementAttributes.FirstOrDefault();

			var isEmpty = xmlReader.IsEmptyElement;
			var hasContainerElement = xmlElementAttribute == null;
			if (hasContainerElement)
			{
				xmlReader.ReadStartElement(parameterName, parameterNs);
			}

			var elementType = xmlElementAttribute?.Type ?? parameterType.GetElementType();

			var arrayItemName = xmlArrayItemAttribute?.ElementName ?? xmlElementAttribute?.ElementName ?? elementType.Name;
			if (xmlArrayItemAttribute?.ElementName == null && elementType.Namespace?.StartsWith("System") == true)
			{
				var compiler = new CSharpCodeProvider();
				var type = new CodeTypeReference(elementType);
				arrayItemName = compiler.GetTypeOutput(type);
			}

			var deserializeMethod = typeof(XmlSerializerExtensions).GetGenericMethod(nameof(XmlSerializerExtensions.DeserializeArray), elementType);
			var arrayItemNamespace = xmlArrayItemAttribute?.Namespace ?? parameterNs;

			var serializer = CachedXmlSerializer.GetXmlSerializer(elementType, arrayItemName, arrayItemNamespace);

			object result = null;

			if (xmlReader.HasValue && elementType?.FullName == "System.Byte")
			{
				result = xmlReader.ReadContentAsBase64();
			}
			else
			{
				result = deserializeMethod.Invoke(null, new object[] { serializer, arrayItemName, arrayItemNamespace, xmlReader });
			}

			if (!isEmpty && hasContainerElement)
			{
				xmlReader.ReadEndElement();
			}

			return result;
		}
	}
}
