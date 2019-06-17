using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	public class MetaWcfXmlBodyWriter : BodyWriter
	{
#pragma warning disable SA1310 // Field names must not contain underscore
		private const string XMLNS_XS = "http://www.w3.org/2001/XMLSchema";
		private const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";
#pragma warning restore SA1310 // Field names must not contain underscore

		//private static int _namespaceCounter = 1;
		private readonly ServiceDescription _service;
		private readonly string _baseUrl;

		private readonly Queue<Type> _enumToBuild;
		private readonly Queue<Type> _complexTypeToBuild;
		private readonly Queue<Type> _arrayToBuild;

		private readonly HashSet<string> _builtEnumTypes;
		private readonly HashSet<string> _builtComplexTypes;
		private readonly HashSet<string> _buildArrayTypes;

		private bool _buildDateTimeOffset;

		public MetaWcfXmlBodyWriter(ServiceDescription service, string baseUrl, Binding binding) : base(isBuffered: true)
		{
			_service = service;
			_baseUrl = baseUrl;

			_enumToBuild = new Queue<Type>();
			_complexTypeToBuild = new Queue<Type>();
			_arrayToBuild = new Queue<Type>();
			_builtEnumTypes = new HashSet<string>();
			_builtComplexTypes = new HashSet<string>();
			_buildArrayTypes = new HashSet<string>();

			// By default in WCF this names must be SOAP
			BindingName = "SOAP";
			PortName = "SOAP";
		}

		private string BindingName { get; }
		private string BindingType => _service.Contracts.First().Name;
		private string PortName { get; }

		private string TargetNameSpace => _service.Contracts.First().Namespace;

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			AddTypes(writer);

			AddMessage(writer);

			AddPortType(writer);

			AddBinding(writer);

			AddService(writer);
		}

		private void WriteParameters(XmlDictionaryWriter writer, SoapMethodParameterInfo[] parameterInfos)
		{
			foreach (var parameterInfo in parameterInfos)
			{
				var elementAttribute = parameterInfo.Parameter.GetCustomAttribute<XmlElementAttribute>();
				var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
										? elementAttribute.ElementName
										: parameterInfo.Parameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameterInfo.Parameter.Name;
				AddSchemaType(writer, parameterInfo.Parameter.ParameterType, parameterName, @namespace: elementAttribute?.Namespace);
			}
		}

		private void AddTypes(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl:types");
			writer.WriteStartElement("xs:schema");
			writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", TargetNameSpace);

			//writer.WriteStartElement("xs:import");
			//writer.WriteAttributeString("namespace", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");
			//writer.WriteEndElement();
			//writer.WriteStartElement("xs:import");
			//writer.WriteAttributeString("namespace", "http://schemas.datacontract.org/2004/07/System");
			//writer.WriteEndElement();
			foreach (var operation in _service.Operations)
			{
				// input parameters of operation
				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", operation.Name);
				writer.WriteStartElement("xs:complexType");
				writer.WriteStartElement("xs:sequence");

				WriteParameters(writer, operation.InParameters);

				writer.WriteEndElement(); // xs:sequence
				writer.WriteEndElement(); // xs:complexType
				writer.WriteEndElement(); // xs:element

				// output parameter / return of operation
				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", operation.Name + "Response");
				writer.WriteStartElement("xs:complexType");
				writer.WriteStartElement("xs:sequence");

				if (operation.DispatchMethod.ReturnType != typeof(void))
				{
					var returnType = operation.DispatchMethod.ReturnType;
					if (returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					{
						returnType = returnType.GetGenericArguments().First();
					}

					var returnName = operation.DispatchMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? operation.Name + "Result";
					AddSchemaType(writer, returnType, returnName);
				}

				WriteParameters(writer, operation.OutParameters);

				writer.WriteEndElement(); // xs:sequence
				writer.WriteEndElement(); // xs:complexType
				writer.WriteEndElement(); // xs:element
			}

			while (_complexTypeToBuild.Count > 0)
			{
				var toBuild = _complexTypeToBuild.Dequeue();
				var isArray = toBuild.IsArray || toBuild.GetCustomAttribute<XmlArrayAttribute>() != null;
				var toBuildName = isArray ? ResolveTypeOfArray(toBuild.Name.Replace("[]", string.Empty))
					: typeof(IEnumerable).IsAssignableFrom(toBuild) ? GetGenericType(toBuild).Name
					: toBuild.Name;

				if (!_builtComplexTypes.Contains(toBuildName))
				{
					writer.WriteStartElement("xs:complexType");
					var baseType = toBuild.BaseType.Name;
					var isDerived = baseType != "Object";

					//if (toBuild.IsArray)
					//{
					//	writer.WriteAttributeString("name", toBuildName);
					//}
					//else if (typeof(IEnumerable).IsAssignableFrom(toBuild))
					//{
					//	writer.WriteAttributeString("name", toBuildName);
					//}
					//else
					//{
					//	writer.WriteAttributeString("name", toBuildName);
					//}
					writer.WriteAttributeString("name", toBuildName);

					if (isDerived)
					{
						writer.WriteStartElement("xs:complexContent");
						writer.WriteAttributeString("mixed", "false");
						writer.WriteStartElement("xs:extension");
						writer.WriteAttributeString("base", $"tns:{baseType}");

						// baseType may not exists in methods list
						if (!_builtComplexTypes.Contains(baseType))
						{
							_complexTypeToBuild.Enqueue(toBuild.BaseType);
						}
					}

					writer.WriteStartElement("xs:sequence");

					if (toBuild.IsArray)
					{
						AddSchemaType(writer, toBuild.GetElementType(), null, true);
					}
					else if (typeof(IEnumerable).IsAssignableFrom(toBuild))
					{
						AddSchemaType(writer, GetGenericType(toBuild), null, false);
					}
					else
					{
						// Get only declared properties not inherited
						var propertiesList = toBuild.GetProperties(BindingFlags.DeclaredOnly |
																   BindingFlags.Public |
																   BindingFlags.Instance).Where(prop =>
							prop.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute"));

						foreach (var property in propertiesList)
						{
							// If this element has XmlArray attribute
							var isArrayElement = property.GetCustomAttribute<XmlArrayAttribute>() != null;
							var elementName = property.GetCustomAttribute<XmlElementAttribute>()?.ElementName;

							AddSchemaType(writer, property.PropertyType, string.IsNullOrEmpty(elementName) ? property.Name : elementName, false, isArrayElement);
						}
					}

					if (isDerived)
					{
						writer.WriteEndElement(); // xs:extension
						writer.WriteEndElement(); // xs:complexContent
					}

					writer.WriteEndElement(); // xs:sequence
					writer.WriteEndElement(); // xs:complexType

					//if (!isIEnumerable)
					//{
					//	writer.WriteStartElement("xs:element");
					//	writer.WriteAttributeString("name", toBuildName);
					//	writer.WriteAttributeString("nillable", "true");
					//	writer.WriteAttributeString("type", "tns:" + toBuildName);
					//	writer.WriteEndElement(); // xs:element
					//}
					_builtComplexTypes.Add(toBuildName);
				}
			}

			while (_enumToBuild.Count > 0)
			{
				Type toBuild = _enumToBuild.Dequeue();

				if (toBuild.IsByRef)
				{
					toBuild = toBuild.GetElementType();
				}

				if (!_builtEnumTypes.Contains(toBuild.Name))
				{
					writer.WriteStartElement("xs:simpleType");
					writer.WriteAttributeString("name", toBuild.Name);
					writer.WriteStartElement("xs:restriction ");
					writer.WriteAttributeString("base", "xs:string");

					foreach (var value in Enum.GetValues(toBuild))
					{
						writer.WriteStartElement("xs:enumeration ");
						writer.WriteAttributeString("value", value.ToString());
						writer.WriteEndElement(); // xs:enumeration
					}

					writer.WriteEndElement(); // xs:restriction
					writer.WriteEndElement(); // xs:simpleType

					_builtEnumTypes.Add(toBuild.Name);
				}
			}

			while (_arrayToBuild.Count > 0)
			{
				var toBuild = _arrayToBuild.Dequeue();
				var genericType = GetGenericType(toBuild);
				var toBuildName = toBuild.IsArray ? ResolveTypeOfArray(toBuild.Name.Replace("[]", string.Empty))
					: typeof(IEnumerable).IsAssignableFrom(toBuild) ? ResolveTypeOfArray(genericType.Name)
					: toBuild.Name;

				if (!_buildArrayTypes.Contains(toBuildName))
				{
					//writer.WriteStartElement("xs:schema");
					//writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
					//writer.WriteAttributeString("xmlns:tns", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");
					//writer.WriteAttributeString("elementFormDefault", "qualified");
					//writer.WriteAttributeString("targetNamespace", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");
					writer.WriteStartElement("xs:complexType");
					writer.WriteAttributeString("name", toBuildName);

					writer.WriteStartElement("xs:sequence");
					AddSchemaType(writer, genericType, null, true);
					writer.WriteEndElement(); // xs:sequence

					writer.WriteEndElement(); // xs:complexType

					//writer.WriteStartElement("xs:element");
					//writer.WriteAttributeString("name", toBuildName);
					//writer.WriteAttributeString("nillable", "true");
					//writer.WriteAttributeString("type", "tns:" + toBuildName);
					//writer.WriteEndElement(); // xs:element
					//writer.WriteEndElement(); // xs:schema
					_buildArrayTypes.Add(toBuildName);
				}
			}

			writer.WriteEndElement(); // xs:schema

			if (_buildDateTimeOffset)
			{
				writer.WriteStartElement("xs:schema");
				writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
				writer.WriteAttributeString("xmlns:tns", "http://schemas.datacontract.org/2004/07/System");
				writer.WriteAttributeString("elementFormDefault", "qualified");
				writer.WriteAttributeString("targetNamespace", "http://schemas.datacontract.org/2004/07/System");

				writer.WriteStartElement("xs:import");
				writer.WriteAttributeString("namespace", "http://schemas.microsoft.com/2003/10/Serialization/");
				writer.WriteEndElement();

				writer.WriteStartElement("xs:complexType");
				writer.WriteAttributeString("name", "DateTimeOffset");
				writer.WriteStartElement("xs:annotation");
				writer.WriteStartElement("xs:appinfo");

				writer.WriteElementString("IsValueType", "http://schemas.microsoft.com/2003/10/Serialization/", "true");
				writer.WriteEndElement(); // xs:appinfo
				writer.WriteEndElement(); // xs:annotation

				writer.WriteStartElement("xs:sequence");
				AddSchemaType(writer, typeof(DateTime), "DateTime");
				AddSchemaType(writer, typeof(short), "OffsetMinutes");
				writer.WriteEndElement(); // xs:sequence

				writer.WriteEndElement(); // xs:complexType

				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", "DateTimeOffset");
				writer.WriteAttributeString("nillable", "true");
				writer.WriteAttributeString("type", "tns:DateTimeOffset");
				writer.WriteEndElement();

				writer.WriteEndElement(); // xs:schema
			}

			writer.WriteEndElement(); // wsdl:types
		}

		private void AddMessage(XmlDictionaryWriter writer)
		{
			foreach (var operation in _service.Operations)
			{
				// input
				writer.WriteStartElement("wsdl:message");
				writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_InputMessage");
				writer.WriteStartElement("wsdl:part");
				writer.WriteAttributeString("name", "parameters");
				writer.WriteAttributeString("element", "tns:" + operation.Name);
				writer.WriteEndElement(); // wsdl:part
				writer.WriteEndElement(); // wsdl:message
										  // output
				writer.WriteStartElement("wsdl:message");
				writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_OutputMessage");
				writer.WriteStartElement("wsdl:part");
				writer.WriteAttributeString("name", "parameters");
				writer.WriteAttributeString("element", "tns:" + operation.Name + "Response");
				writer.WriteEndElement(); // wsdl:part
				writer.WriteEndElement(); // wsdl:message
			}
		}

		private void AddPortType(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl:portType");
			writer.WriteAttributeString("name", BindingType);
			foreach (var operation in _service.Operations)
			{
				writer.WriteStartElement("wsdl:operation");
				writer.WriteAttributeString("name", operation.Name);
				writer.WriteStartElement("wsdl:input");
				writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_InputMessage");
				writer.WriteEndElement(); // wsdl:input
				writer.WriteStartElement("wsdl:output");
				writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_OutputMessage");
				writer.WriteEndElement(); // wsdl:output
				writer.WriteEndElement(); // wsdl:operation
			}

			writer.WriteEndElement(); // wsdl:portType
		}

		private void AddBinding(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl:binding");
			writer.WriteAttributeString("name", BindingName);
			writer.WriteAttributeString("type", "tns:" + BindingType);

			writer.WriteStartElement("soap:binding");
			writer.WriteAttributeString("transport", TRANSPORT_SCHEMA);
			writer.WriteEndElement(); // soap:binding

			foreach (var operation in _service.Operations)
			{
				writer.WriteStartElement("wsdl:operation");
				writer.WriteAttributeString("name", operation.Name);

				writer.WriteStartElement("soap:operation");
				writer.WriteAttributeString("soapAction", operation.SoapAction);
				writer.WriteAttributeString("style", "document");
				writer.WriteEndElement(); // soap:operation

				writer.WriteStartElement("wsdl:input");
				writer.WriteStartElement("soap:body");
				writer.WriteAttributeString("use", "literal");
				writer.WriteEndElement(); // soap:body
				writer.WriteEndElement(); // wsdl:input

				writer.WriteStartElement("wsdl:output");
				writer.WriteStartElement("soap:body");
				writer.WriteAttributeString("use", "literal");
				writer.WriteEndElement(); // soap:body
				writer.WriteEndElement(); // wsdl:output

				writer.WriteEndElement(); // wsdl:operation
			}

			writer.WriteEndElement(); // wsdl:binding
		}

		private void AddService(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl:service");
			writer.WriteAttributeString("name", _service.ServiceType.Name);

			writer.WriteStartElement("wsdl:port");
			writer.WriteAttributeString("name", PortName);
			writer.WriteAttributeString("binding", "tns:" + BindingName);

			writer.WriteStartElement("soap:address");

			writer.WriteAttributeString("location", _baseUrl);
			writer.WriteEndElement(); // soap:address

			writer.WriteEndElement(); // wsdl:port
		}

		private void AddSchemaType(XmlDictionaryWriter writer, Type type, string name, bool isArray = false, bool isArrayElement = false, string @namespace = null)
		{
			var typeInfo = type.GetTypeInfo();
			if (typeInfo.IsByRef)
			{
				type = typeInfo.GetElementType();
			}

			writer.WriteStartElement("xs:element");

			// Check for null, since we may use empty NS
			if (@namespace != null)
			{
				writer.WriteAttributeString("targetNamespace", @namespace);
			}
			else if (typeInfo.IsValueType && typeInfo.BaseType.Namespace.StartsWith("System"))
			{
				string xsTypename;
				if (typeof(DateTimeOffset).IsAssignableFrom(type))
				{
					if (string.IsNullOrEmpty(name))
					{
						name = type.Name;
					}

					xsTypename = "nsdto:" + type.Name;
					writer.WriteAttributeString("xmlns:nsdto", "http://schemas.datacontract.org/2004/07/System");

					_buildDateTimeOffset = true;
				}
				else if (typeInfo.IsEnum)
				{
					xsTypename = "tns:" + type.Name;
					_enumToBuild.Enqueue(type);
				}
				else
				{
					var underlyingType = Nullable.GetUnderlyingType(type);
					if (underlyingType != null)
					{
						xsTypename = ResolveType(underlyingType);
						writer.WriteAttributeString("nillable", "true");
					}
					else
					{
						xsTypename = ResolveType(type);
					}
				}

				if (isArray)
				{
					writer.WriteAttributeString("minOccurs", "0");
					writer.WriteAttributeString("maxOccurs", "unbounded");
					writer.WriteAttributeString("nillable", "true");
				}
				else
				{
					writer.WriteAttributeString("minOccurs", "1");
					writer.WriteAttributeString("maxOccurs", "1");
				}

				if (string.IsNullOrEmpty(name))
				{
					name = xsTypename.Split(':')[1];
				}

				writer.WriteAttributeString("name", name);
				writer.WriteAttributeString("type", xsTypename);
			}
			else
			{
				writer.WriteAttributeString("minOccurs", "0");
				if (isArray)
				{
					writer.WriteAttributeString("nillable", "true");
					writer.WriteAttributeString("maxOccurs", "unbounded");
				}

				if (type.Name == "String" || type.Name == "String&")
				{
					writer.WriteAttributeString("maxOccurs", "1");
					if (string.IsNullOrEmpty(name))
					{
						name = "string";
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "xs:string");
				}
				else if (type == typeof(System.Xml.Linq.XElement))
				{
					writer.WriteAttributeString("maxOccurs", "1");
					writer.WriteAttributeString("name", name);

					writer.WriteStartElement("xs:complexType");
					writer.WriteAttributeString("mixed", "true");
					writer.WriteStartElement("xs:sequence");
					writer.WriteStartElement("xs:any");
					writer.WriteEndElement();
					writer.WriteEndElement();
					writer.WriteEndElement();
				}
				else if (type.Name == "Byte[]")
				{
					writer.WriteAttributeString("maxOccurs", "1");
					if (string.IsNullOrEmpty(name))
					{
						name = "base64Binary";
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "xs:base64Binary");
				}
				else if (type.IsArray)
				{
					if (string.IsNullOrEmpty(name))
					{
						name = type.Name;
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "tns:" + ResolveTypeOfArray(type.Name.Replace("[]", string.Empty)));

					_arrayToBuild.Enqueue(type);
				}
				else if (typeof(IEnumerable).IsAssignableFrom(type))
				{
					var genericType = GetGenericType(type);

					if (genericType.Name == "String")
					{
						if (string.IsNullOrEmpty(name))
						{
							name = type.Name;
						}

						writer.WriteAttributeString("maxOccurs", "unbounded");
						writer.WriteAttributeString("name", name);
						writer.WriteAttributeString("type", "xs:string");
					}
					else
					{
						if (string.IsNullOrEmpty(name))
						{
							name = type.Name;
						}

						writer.WriteAttributeString("name", name);

						if (!isArray && !isArrayElement)
						{
							writer.WriteAttributeString("maxOccurs", "unbounded");
						}

						if (isArray || isArrayElement)
						{
							writer.WriteAttributeString("type", "tns:" + ResolveTypeOfArray(genericType.Name));
							_complexTypeToBuild.Enqueue(genericType);
							_arrayToBuild.Enqueue(type);
						}
						else if (genericType.IsPrimitive)
						{
							writer.WriteAttributeString("type", ResolveType(genericType));
						}
						else
						{
							writer.WriteAttributeString("type", "tns:" + genericType.Name);
							_complexTypeToBuild.Enqueue(genericType);
						}
					}
				}
				else
				{
					if (string.IsNullOrEmpty(name))
					{
						name = type.Name;
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "tns:" + type.Name);

					_complexTypeToBuild.Enqueue(type);
				}
			}

			writer.WriteEndElement(); // xs:element
		}

		private string ResolveType(Type type)
		{
			string typeName = type.IsEnum ? type.GetEnumUnderlyingType().Name : type.Name;
			string resolvedType = null;

			switch (typeName)
			{
				case "Boolean":
					resolvedType = "xs:boolean";
					break;
				case "Byte":
					resolvedType = "xs:unsignedByte";
					break;
				case "Int16":
					resolvedType = "xs:short";
					break;
				case "Int32":
					resolvedType = "xs:int";
					break;
				case "Int64":
					resolvedType = "xs:long";
					break;
				case "SByte":
					resolvedType = "xs:byte";
					break;
				case "UInt16":
					resolvedType = "xs:unsignedShort";
					break;
				case "UInt32":
					resolvedType = "xs:unsignedInt";
					break;
				case "UInt64":
					resolvedType = "xs:unsignedLong";
					break;
				case "Decimal":
					resolvedType = "xs:decimal";
					break;
				case "Double":
					resolvedType = "xs:double";
					break;
				case "Single":
					resolvedType = "xs:float";
					break;
				case "DateTime":
					resolvedType = "xs:dateTime";
					break;
				case "Guid":
					resolvedType = "xs:string";
					break;
				case "Char":
					resolvedType = "xs:string";
					break;
				case "TimeSpan":
					resolvedType = "xs:duration";
					break;
			}

			if (string.IsNullOrEmpty(resolvedType))
			{
				throw new ArgumentException($".NET type {typeName} cannot be resolved into XML schema type");
			}

			return resolvedType;
		}

		private Type GetGenericType(Type collectionType)
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

		private string ResolveTypeOfArray(string name)
		{
			if (name == "Int16")
			{
				name = "Short";
			}
			else if (name == "Int32")
			{
				name = "Int";
			}
			else if (name == "Int64")
			{
				name = "Long";
			}
			else if (name == "UInt16")
			{
				name = "UnsignedShort";
			}
			else if (name == "UInt32")
			{
				name = "UnsignedInt";
			}
			else if (name == "UInt64")
			{
				name = "UnsignedLong";
			}
			else if (name == "Single")
			{
				name = "Float";
			}
			else if (name == "TimeSpan")
			{
				name = "Duration";
			}
			else if (name == "Object")
			{
				name = "AnyType";
			}

			return "ArrayOf" + name;
		}
	}
}
