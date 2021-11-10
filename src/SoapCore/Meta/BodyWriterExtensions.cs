using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SoapCore.Meta
{
	public static class BodyWriterExtensions
	{
		//switches to easily revert to previous behaviour if there is a problem
		private static readonly bool UseXmlSchemaProvider = true;
		private static readonly bool UseXmlReflectionImporter = false;
		public static bool TryAddSchemaTypeFromXmlSchemaProviderAttribute(this XmlDictionaryWriter writer, Type type, string name, SoapSerializer serializer, XmlNamespaceManager xmlNamespaceManager = null, bool isUnqualified = false)
		{
			if (!UseXmlSchemaProvider && !UseXmlReflectionImporter)
			{
				return false;
			}

			if (UseXmlReflectionImporter)
			{
				var schemas = new XmlSchemas();
				var xmlImporter = new XmlReflectionImporter();
				var exporter = new XmlSchemaExporter(schemas);

				var xmlTypeMapping = xmlImporter.ImportTypeMapping(type, new XmlRootAttribute() { ElementName = name });
				exporter.ExportTypeMapping(xmlTypeMapping);
				schemas.Compile(null, true);

				using var memoryStream = new MemoryStream();
				foreach (XmlSchema schema in schemas)
				{
					schema.Write(memoryStream);
				}

				memoryStream.Position = 0;

				var streamReader = new StreamReader(memoryStream);
				var result = streamReader.ReadToEnd();

				var doc = new XmlDocument();
				doc.LoadXml(result);
				doc.DocumentElement.WriteContentTo(writer);

				return true;
			}

			var xmlSchemaSet = xmlNamespaceManager == null ? new XmlSchemaSet() : new XmlSchemaSet(xmlNamespaceManager.NameTable);
			var xmlSchemaProviderAttribute = type.GetCustomAttribute<XmlSchemaProviderAttribute>(true);
			if (xmlSchemaProviderAttribute != null && true)
			{
				XmlSchema schema = new XmlSchema();
				if (xmlNamespaceManager != null)
				{
					schema.Namespaces = xmlNamespaceManager.Convert();
				}

				if (xmlSchemaProviderAttribute.IsAny)
				{
					//MetaWCFBodyWriter usage....
					//writer.WriteAttributeString("name", name);
					//writer.WriteAttributeString("nillable", "true");
					//writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
					//writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);
					//writer.WriteStartElement("xs", "any", Namespaces.XMLNS_XSD);
					//writer.WriteAttributeString("minOccurs", "0");
					//writer.WriteAttributeString("processContents", "lax");
					//writer.WriteEndElement();
					//writer.WriteEndElement();
					//writer.WriteEndElement();
					var sequence = new XmlSchemaSequence();
					sequence.Items.Add(new XmlSchemaAny() { ProcessContents = XmlSchemaContentProcessing.Lax });
					var complex = new XmlSchemaComplexType()
					{
						Particle = sequence
					};
					var element = new XmlSchemaElement()
					{
						MinOccurs = 0,
						MaxOccurs = 1,
						Name = name,
						IsNillable = serializer == SoapSerializer.DataContractSerializer,
						SchemaType = complex
					};
					if (isUnqualified)
					{
						element.Form = XmlSchemaForm.Unqualified;
					}

					schema.Items.Add(element);
				}
				else
				{
					var methodInfo = type.GetMethod(xmlSchemaProviderAttribute.MethodName, BindingFlags.Static | BindingFlags.Public);
					var xmlSchemaInfoObject = methodInfo.Invoke(null, new object[] { xmlSchemaSet });
					var element = new XmlSchemaElement()
					{
						MinOccurs = 0,
						MaxOccurs = 1,
						Name = name,
					};

					if (xmlSchemaInfoObject is XmlQualifiedName xmlQualifiedName)
					{
						element.SchemaTypeName = xmlQualifiedName;
					}
					else if (xmlSchemaInfoObject is XmlSchemaType xmlSchemaType)
					{
						element.SchemaType = xmlSchemaType;
					}
					else
					{
						throw new InvalidOperationException($"Invalid {nameof(xmlSchemaInfoObject)} type: {xmlSchemaInfoObject.GetType()}");
					}

					if (isUnqualified)
					{
						element.Form = XmlSchemaForm.Unqualified;
					}

					schema.Items.Add(element);
				}

				using var memoryStream = new MemoryStream();
				schema.Write(memoryStream);
				memoryStream.Position = 0;

				var streamReader = new StreamReader(memoryStream);
				var result = streamReader.ReadToEnd();

				var doc = new XmlDocument();
				doc.LoadXml(result);
				doc.DocumentElement.WriteContentTo(writer);

				return true;
			}

			return false;
		}

		public static bool IsChoice(this MemberInfo member)
		{
			var choiceItem = member.GetCustomAttribute<XmlChoiceIdentifierAttribute>();
			return choiceItem != null || member.GetCustomAttributes<XmlElementAttribute>().Count() > 1;
		}

		public static bool IsAttribute(this MemberInfo member)
		{
			var attributeItem = member.GetCustomAttribute<XmlAttributeAttribute>();
			return attributeItem != null;
		}

		public static bool IsIgnored(this MemberInfo member)
		{
			return member
				.CustomAttributes
				.Any(attr =>
					attr.AttributeType == typeof(IgnoreDataMemberAttribute) ||
					attr.AttributeType == typeof(XmlIgnoreAttribute));
		}

		public static bool IsEnumerableType(this Type collectionType)
		{
			if (collectionType.IsArray)
			{
				return true;
			}

			return typeof(IEnumerable).IsAssignableFrom(collectionType);
		}

		public static Type GetGenericType(this Type collectionType)
		{
			// Recursively look through the base class to find the Generic Type of the Enumerable
			var baseType = collectionType;
			var baseTypeInfo = collectionType.GetTypeInfo();
			while (!baseTypeInfo.IsGenericType && baseTypeInfo.BaseType != null)
			{
				baseType = baseTypeInfo.BaseType;
				baseTypeInfo = baseType.GetTypeInfo();
			}

			return baseType.GetTypeInfo().GetGenericArguments().DefaultIfEmpty(typeof(object)).FirstOrDefault();
		}

		public static string GetSerializedTypeName(this Type type)
		{
			var namedType = type;
			bool isNullableArray = false;
			if (type.IsArray)
			{
				namedType = type.GetElementType();
				var underlyingType = Nullable.GetUnderlyingType(namedType);
				if (underlyingType != null)
				{
					namedType = underlyingType;
					isNullableArray = true;
				}
			}
			else if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
			{
				namedType = GetGenericType(type);
				var underlyingType = Nullable.GetUnderlyingType(namedType);
				if (underlyingType != null)
				{
					namedType = underlyingType;
					isNullableArray = true;
				}
			}

			string typeName = namedType.Name;
			var xmlTypeAttribute = namedType.GetCustomAttribute<XmlTypeAttribute>(true);
			if (xmlTypeAttribute != null && !string.IsNullOrWhiteSpace(xmlTypeAttribute.TypeName))
			{
				typeName = xmlTypeAttribute.TypeName;
			}

			if (type.IsArray || (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType))
			{
				if (namedType.IsArray || (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType))
				{
					typeName = GetSerializedTypeName(namedType);
				}

				typeName = GetArrayTypeName(typeName.Replace("[]", string.Empty), isNullableArray);
			}

			return typeName;
		}

		private static string GetArrayTypeName(string typeName, bool isNullable)
		{
			return "ArrayOf" + (isNullable ? "Nullable" : null) + (ClrTypeResolver.ResolveOrDefault(typeName).FirstCharToUpperOrDefault() ?? typeName);
		}

		private static XmlSerializerNamespaces Convert(this XmlNamespaceManager xmlNamespaceManager)
		{
			XmlSerializerNamespaces xmlSerializerNamespaces = new XmlSerializerNamespaces();
			foreach (var ns in xmlNamespaceManager.GetNamespacesInScope(XmlNamespaceScope.Local))
			{
				xmlSerializerNamespaces.Add(ns.Key, ns.Value);
			}

			return xmlSerializerNamespaces;
		}

		private static string FirstCharToUpperOrDefault(this string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return input;
			}

			return input.First().ToString().ToUpper() + input.Substring(1);
		}
	}
}
