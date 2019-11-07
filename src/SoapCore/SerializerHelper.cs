using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.CSharp;
using SoapCore.ServiceModel;

namespace SoapCore
{
	internal class SerializerHelper
	{
		private readonly SoapSerializer _serializer;

		public SerializerHelper(SoapSerializer serializer)
		{
			_serializer = serializer;
		}

		public object DeserializeInputParameter(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, SoapMethodParameterInfo parameterInfo = null)
		{
			if (xmlReader.IsStartElement(parameterName, parameterNs))
			{
				xmlReader.MoveToStartElement(parameterName, parameterNs);

				if (xmlReader.IsStartElement(parameterName, parameterNs))
				{
					switch (_serializer)
					{
						case SoapSerializer.XmlSerializer:
							if (!parameterType.IsArray || (parameterInfo != null && parameterInfo.ArrayName != null && parameterInfo.ArrayItemName == null))
							{
								// case [XmlElement("parameter")] int parameter
								// case int[] parameter
								// case [XmlArray("parameter")] int[] parameter
								return DeserializeObject(xmlReader, parameterType, parameterName, parameterNs);
							}
							else
							{
								// case [XmlElement("parameter")] int[] parameter
								// case [XmlArray("parameter"), XmlArrayItem(ElementName = "item")] int[] parameter
								return DeserializeArray(xmlReader, parameterType, parameterName, parameterNs, parameterInfo);
							}

						case SoapSerializer.DataContractSerializer:
							return DeserializeDataContract(xmlReader, parameterType, parameterName, parameterNs);

						default:
							throw new NotImplementedException();
					}
				}
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

			lock (serializer)
			{
				if (elementType == typeof(Stream) || typeof(Stream).IsAssignableFrom(elementType))
				{
					xmlReader.Read();
					return new MemoryStream(xmlReader.ReadContentAsBase64());
				}

				return serializer.Deserialize(xmlReader);
			}
		}

		private static object DeserializeDataContract(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs)
		{
			var elementType = parameterType.GetElementType();

			if (elementType == null || parameterType.IsArray)
			{
				elementType = parameterType;
			}

			var serializer = new DataContractSerializer(elementType, parameterName, parameterNs);

			return serializer.ReadObject(xmlReader, verifyObjectName: true);
		}

		private object DeserializeArray(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, SoapMethodParameterInfo parameterInfo)
		{
			//if (parameterInfo.ArrayItemName != null)
			{
				xmlReader.ReadStartElement(parameterName, parameterNs);
			}

			var elementType = parameterType.GetElementType();

			var localName = parameterInfo.ArrayItemName ?? elementType.Name;
			if (parameterInfo.ArrayItemName == null && elementType.Namespace.StartsWith("System"))
			{
				var compiler = new CSharpCodeProvider();
				var type = new CodeTypeReference(elementType);
				localName = compiler.GetTypeOutput(type);
			}

			//localName = "ComplexModelInput";
			var deserializeMethod = typeof(XmlSerializerExtensions).GetGenericMethod(nameof(XmlSerializerExtensions.DeserializeArray), elementType);
			var serializer = CachedXmlSerializer.GetXmlSerializer(elementType, localName, parameterNs);

			object result = null;

			lock (serializer)
			{
				result = deserializeMethod.Invoke(null, new object[] { serializer, localName, parameterNs, xmlReader });
			}

			//if (parameterInfo.ArrayItemName != null)
			{
				xmlReader.ReadEndElement();
			}

			return result;
		}
	}
}
