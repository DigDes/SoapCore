using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Microsoft.CSharp;

namespace SoapCore
{
	internal class SerializerHelper
	{
		private readonly SoapSerializer _serializer;
		private readonly Type[] _excludedPrimitiveTypesForArrayWithoutWrapping = new Type[] { typeof(float), typeof(double), typeof(IntPtr), typeof(UIntPtr) };

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
							if (!parameterType.IsArray)
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
				return Array.CreateInstance(parameterType.GetElementType(), 0);
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

		private object DeserializeArrayXmlSerializer(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, ICustomAttributeProvider customAttributeProvider)
		{
			var xmlArrayAttributes = customAttributeProvider.GetCustomAttributes(typeof(XmlArrayItemAttribute), true);
			XmlArrayItemAttribute xmlArrayItemAttribute = xmlArrayAttributes.FirstOrDefault() as XmlArrayItemAttribute;
			var xmlElementAttributes = customAttributeProvider.GetCustomAttributes(typeof(XmlElementAttribute), true);
			XmlElementAttribute xmlElementAttribute = xmlElementAttributes.FirstOrDefault() as XmlElementAttribute;

			var isEmpty = xmlReader.IsEmptyElement;
			var hasContainerElement = xmlElementAttribute == null;
			if (hasContainerElement)
			{
				xmlReader.ReadStartElement(parameterName, parameterNs);
			}

			var elementType = parameterType.GetElementType();

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
			else if (xmlReader.HasValue && elementType.IsPrimitive && !_excludedPrimitiveTypesForArrayWithoutWrapping.Contains(elementType))
			{
				var values = xmlReader.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				result = CastPrimitiveArray(values, elementType);
				xmlReader.Skip();
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

		//Cast any primitive type, except double, float, IntPtr and UIntPtr
		private object CastPrimitiveArray(IEnumerable<string> values, Type elementType)
		{
			if (elementType == typeof(bool))
			{
				return CastArray<bool>(values);
			}
			else if (elementType == typeof(byte))
			{
				return CastArray<byte>(values);
			}
			else if (elementType == typeof(sbyte))
			{
				return CastArray<sbyte>(values);
			}
			else if (elementType == typeof(short))
			{
				return CastArray<short>(values);
			}
			else if (elementType == typeof(ushort))
			{
				return CastArray<ushort>(values);
			}
			else if (elementType == typeof(int))
			{
				return CastArray<int>(values);
			}
			else if (elementType == typeof(uint))
			{
				return CastArray<uint>(values);
			}
			else if (elementType == typeof(long))
			{
				return CastArray<long>(values);
			}
			else if (elementType == typeof(ulong))
			{
				return CastArray<ulong>(values);
			}

			return null;
		}

		private T[] CastArray<T>(IEnumerable<string> input)
		{
			return input.Select(x => Cast<T>(x)).ToArray();
		}

		private T Cast<T>(string input)
		{
			return (T)Convert.ChangeType(input, typeof(T));
		}
	}
}
