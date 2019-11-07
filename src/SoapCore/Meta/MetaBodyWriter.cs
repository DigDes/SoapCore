using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using SoapCore.ServiceModel;

namespace SoapCore.Meta
{
	internal class MetaBodyWriter : BodyWriter
	{
#pragma warning disable SA1310 // Field names must not contain underscore
		private const string XMLNS_XS = "http://www.w3.org/2001/XMLSchema";
		private const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";
#pragma warning restore SA1310 // Field names must not contain underscore

		private static int _namespaceCounter = 1;

		private readonly ServiceDescription _service;
		private readonly string _baseUrl;

		private readonly Queue<Type> _enumToBuild;
		private readonly Queue<Type> _complexTypeToBuild;
		private readonly Queue<Type> _arrayToBuild;

		private readonly HashSet<string> _builtEnumTypes;
		private readonly HashSet<string> _builtComplexTypes;
		private readonly HashSet<string> _buildArrayTypes;

		private readonly Dictionary<Type, Type> _wrappedTypes;

		private bool _buildDateTimeOffset;

		public MetaBodyWriter(ServiceDescription service, string baseUrl, Binding binding) : base(isBuffered: true)
		{
			_service = service;
			_baseUrl = baseUrl;

			_enumToBuild = new Queue<Type>();
			_complexTypeToBuild = new Queue<Type>();
			_arrayToBuild = new Queue<Type>();
			_builtEnumTypes = new HashSet<string>();
			_builtComplexTypes = new HashSet<string>();
			_buildArrayTypes = new HashSet<string>();

			_wrappedTypes = new Dictionary<Type, Type>();

			if (binding != null)
			{
				BindingName = binding.Name;
				PortName = binding.Name;
			}
			else
			{
				BindingName = "BasicHttpBinding_" + _service.Contracts.First().Name;
				PortName = "BasicHttpBinding_" + _service.Contracts.First().Name;
			}
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

		private static string ResolveType(Type type)
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

		private static Type GetGenericType(Type collectionType)
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

		private static bool IsWrappedMessageContractType(Type type)
		{
			var messageContractAttribute = type.GetCustomAttribute<MessageContractAttribute>();

			if (messageContractAttribute != null)
			{
				return messageContractAttribute.IsWrapped;
			}

			return false;
		}

		private static Type GetMessageContractBodyType(Type type)
		{
			var messageContractAttribute = type.GetCustomAttribute<MessageContractAttribute>();

			if (messageContractAttribute != null && !messageContractAttribute.IsWrapped)
			{
				var messageBodyMembers =
					type
						.GetPropertyOrFieldMembers()
						.Select(mi => new
						{
							Member = mi,
							MessageBodyMemberAttribute = mi.GetCustomAttribute<MessageBodyMemberAttribute>()
						})
						.OrderBy(x => x.MessageBodyMemberAttribute.Order)
						.ToList();

				return messageBodyMembers[0].Member.GetPropertyOrFieldType();
			}

			return type;
		}

		private void WriteParameters(XmlDictionaryWriter writer, SoapMethodParameterInfo[] parameterInfos, bool isMessageContract)
		{
			var hasWrittenSchema = false;

			foreach (var parameterInfo in parameterInfos)
			{
				var doWriteInlineType = true;

				if (isMessageContract)
				{
					doWriteInlineType = IsWrappedMessageContractType(parameterInfo.Parameter.ParameterType);
				}

				if (doWriteInlineType)
				{
					if (!hasWrittenSchema)
					{
						writer.WriteStartElement("xs:complexType");
						writer.WriteStartElement("xs:sequence");

						hasWrittenSchema = true;
					}

					var elementAttribute = parameterInfo.Parameter.GetCustomAttribute<XmlElementAttribute>();
					var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
						? elementAttribute.ElementName
						: parameterInfo.Parameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameterInfo.Parameter.Name;

					AddSchemaType(writer, parameterInfo.Parameter.ParameterType, parameterName, @namespace: elementAttribute?.Namespace);
				}
				else
				{
					var messageBodyType = GetMessageContractBodyType(parameterInfo.Parameter.ParameterType);

					writer.WriteAttributeString("type", "tns:" + messageBodyType.Name);
					_complexTypeToBuild.Enqueue(parameterInfo.Parameter.ParameterType);
				}
			}

			if (hasWrittenSchema)
			{
				writer.WriteEndElement(); // xs:sequence
				writer.WriteEndElement(); // xs:complexType
			}
		}

		private void AddTypes(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl:types");
			writer.WriteStartElement("xs:schema");
			writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", TargetNameSpace);

			writer.WriteStartElement("xs:import");
			writer.WriteAttributeString("namespace", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");
			writer.WriteEndElement();

			writer.WriteStartElement("xs:import");
			writer.WriteAttributeString("namespace", "http://schemas.datacontract.org/2004/07/System");
			writer.WriteEndElement();

			foreach (var operation in _service.Operations)
			{
				// input parameters of operation
				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", operation.Name);

				WriteParameters(writer, operation.InParameters, operation.IsMessageContractRequest);

				writer.WriteEndElement(); // xs:element

				// output parameter / return of operation
				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", operation.Name + "Response");

				if (operation.DispatchMethod.ReturnType != typeof(void))
				{
					var returnType = operation.DispatchMethod.ReturnType;
					if (returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					{
						returnType = returnType.GetGenericArguments().First();
					}

					var doWriteInlineType = true;

					if (operation.IsMessageContractResponse)
					{
						doWriteInlineType = IsWrappedMessageContractType(returnType);
					}

					if (doWriteInlineType)
					{
						var returnName = operation.DispatchMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? operation.Name + "Result";
						AddSchemaType(writer, returnType, returnName);
					}
					else
					{
						var type = GetMessageContractBodyType(returnType);

						writer.WriteAttributeString("type", "tns:" + type.Name);
						_complexTypeToBuild.Enqueue(returnType);
					}
				}

				WriteParameters(writer, operation.OutParameters, operation.IsMessageContractResponse);

				writer.WriteEndElement(); // xs:element
			}

			while (_complexTypeToBuild.Count > 0)
			{
				var toBuild = _complexTypeToBuild.Dequeue();

				var toBuildBodyType = GetMessageContractBodyType(toBuild);
				var isWrappedBodyType = IsWrappedMessageContractType(toBuild);

				var toBuildName = toBuildBodyType.IsArray ? "ArrayOf" + toBuildBodyType.Name.Replace("[]", string.Empty)
					: typeof(IEnumerable).IsAssignableFrom(toBuildBodyType) ? "ArrayOf" + GetGenericType(toBuildBodyType).Name
					: toBuildBodyType.Name;

				if (!_builtComplexTypes.Contains(toBuildName))
				{
					writer.WriteStartElement("xs:complexType");
					if (toBuild.IsArray)
					{
						writer.WriteAttributeString("name", toBuildName);
					}
					else if (typeof(IEnumerable).IsAssignableFrom(toBuild))
					{
						writer.WriteAttributeString("name", toBuildName);
					}
					else
					{
						writer.WriteAttributeString("name", toBuildName);
					}

					writer.WriteStartElement("xs:sequence");

					if (toBuild.IsArray)
					{
						AddSchemaType(writer, toBuild.GetElementType(), null, true);
					}
					else if (typeof(IEnumerable).IsAssignableFrom(toBuild))
					{
						AddSchemaType(writer, GetGenericType(toBuild), null, true);
					}
					else
					{
						if (!isWrappedBodyType)
						{
							foreach (var property in toBuildBodyType.GetProperties().Where(prop => !prop.CustomAttributes.Any(attr => attr.AttributeType == typeof(IgnoreDataMemberAttribute))))
							{
								AddSchemaType(writer, property.PropertyType, property.Name);
							}
						}
						else
						{
							foreach (var property in toBuild.GetProperties().Where(prop => !prop.CustomAttributes.Any(attr => attr.AttributeType == typeof(IgnoreDataMemberAttribute))))
							{
								AddSchemaType(writer, property.PropertyType, property.Name);
							}

							var messageBodyMemberFields = toBuild.GetFields()
								.Where(field => field.CustomAttributes.Any(attr => attr.AttributeType == typeof(MessageBodyMemberAttribute)))
								.OrderBy(field => field.GetCustomAttribute<MessageBodyMemberAttribute>().Order);

							foreach (var field in messageBodyMemberFields)
							{
								var messageBodyMember = field.GetCustomAttribute<MessageBodyMemberAttribute>();

								var fieldName = messageBodyMember.Name ?? field.Name;

								AddSchemaType(writer, field.FieldType, fieldName);
							}
						}
					}

					writer.WriteEndElement(); // xs:sequence
					writer.WriteEndElement(); // xs:complexType

					if (isWrappedBodyType)
					{
						writer.WriteStartElement("xs:element");
						writer.WriteAttributeString("name", toBuildName);
						writer.WriteAttributeString("nillable", "true");
						writer.WriteAttributeString("type", "tns:" + toBuildName);
						writer.WriteEndElement(); // xs:element
					}

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

			writer.WriteEndElement(); // xs:schema

			while (_arrayToBuild.Count > 0)
			{
				var toBuild = _arrayToBuild.Dequeue();
				var toBuildName = toBuild.IsArray ? "ArrayOf" + toBuild.Name.Replace("[]", string.Empty)
					: typeof(IEnumerable).IsAssignableFrom(toBuild) ? "ArrayOf" + GetGenericType(toBuild).Name.ToLower()
					: toBuild.Name;

				if (!_buildArrayTypes.Contains(toBuildName))
				{
					writer.WriteStartElement("xs:schema");
					writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
					writer.WriteAttributeString("xmlns:tns", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");
					writer.WriteAttributeString("elementFormDefault", "qualified");
					writer.WriteAttributeString("targetNamespace", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");

					writer.WriteStartElement("xs:complexType");
					writer.WriteAttributeString("name", toBuildName);

					writer.WriteStartElement("xs:sequence");
					AddSchemaType(writer, GetGenericType(toBuild), null, true);
					writer.WriteEndElement(); // xs:sequence

					writer.WriteEndElement(); // xs:complexType

					writer.WriteStartElement("xs:element");
					writer.WriteAttributeString("name", toBuildName);
					writer.WriteAttributeString("nillable", "true");
					writer.WriteAttributeString("type", "tns:" + toBuildName);
					writer.WriteEndElement(); // xs:element

					writer.WriteEndElement(); // xs:schema

					_buildArrayTypes.Add(toBuildName);
				}
			}

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
				AddSchemaType(writer, typeof(DateTime), "DateTime", false);
				AddSchemaType(writer, typeof(short), "OffsetMinutes", false);
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
				var requestTypeName = operation.Name;

				if (operation.IsMessageContractRequest && operation.InParameters.Length > 0)
				{
					if (!IsWrappedMessageContractType(operation.InParameters[0].Parameter.ParameterType))
					{
						requestTypeName = GetMessageContractBodyType(operation.InParameters[0].Parameter.ParameterType).Name;
					}
				}

				writer.WriteStartElement("wsdl:message");
				writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_InputMessage");
				writer.WriteStartElement("wsdl:part");
				writer.WriteAttributeString("name", "parameters");
				writer.WriteAttributeString("element", "tns:" + requestTypeName);
				writer.WriteEndElement(); // wsdl:part
				writer.WriteEndElement(); // wsdl:message

				var responseTypeName = operation.Name + "Response";

				if (operation.DispatchMethod.ReturnType != typeof(void))
				{
					var returnType = operation.DispatchMethod.ReturnType;

					if (returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					{
						returnType = returnType.GetGenericArguments().First();
					}

					if (!IsWrappedMessageContractType(returnType))
					{
						responseTypeName = GetMessageContractBodyType(returnType).Name;
					}
				}

				if (operation.IsMessageContractResponse && operation.OutParameters.Length > 0)
				{
					if (!IsWrappedMessageContractType(operation.OutParameters[0].Parameter.ParameterType))
					{
						responseTypeName = GetMessageContractBodyType(operation.OutParameters[0].Parameter.ParameterType).Name;
					}
				}

				// output
				writer.WriteStartElement("wsdl:message");
				writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_OutputMessage");
				writer.WriteStartElement("wsdl:part");
				writer.WriteAttributeString("name", "parameters");
				writer.WriteAttributeString("element", "tns:" + responseTypeName);
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

		private void AddSchemaType(XmlDictionaryWriter writer, Type type, string name, bool isArray = false, string @namespace = null)
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
			else if (typeInfo.IsValueType && typeInfo.Namespace.StartsWith("System"))
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
					writer.WriteAttributeString("maxOccurs", "unbounded");
					writer.WriteAttributeString("nillable", "true");
				}
				else
				{
					writer.WriteAttributeString("maxOccurs", "1");
				}

				if (type.Name == "String" || type.Name == "String&")
				{
					if (string.IsNullOrEmpty(name))
					{
						name = "string";
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "xs:string");
				}
				else if (type == typeof(System.Xml.Linq.XElement))
				{
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
					if (string.IsNullOrEmpty(name))
					{
						name = "base64Binary";
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "xs:base64Binary");
				}
				else if (type == typeof(Stream) || typeof(Stream).IsAssignableFrom(type))
				{
					name = "StreamBody";

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
					writer.WriteAttributeString("type", "tns:ArrayOf" + type.Name.Replace("[]", string.Empty));

					_complexTypeToBuild.Enqueue(type);
				}
				else if (typeof(IEnumerable).IsAssignableFrom(type))
				{
					if (GetGenericType(type).Name == "String")
					{
						if (string.IsNullOrEmpty(name))
						{
							name = type.Name;
						}

						var ns = $"q{_namespaceCounter++}";

						writer.WriteAttributeString($"xmlns:{ns}", "http://schemas.microsoft.com/2003/10/Serialization/Arrays");
						writer.WriteAttributeString("name", name);
						writer.WriteAttributeString("nillable", "true");

						writer.WriteAttributeString("type", $"{ns}:ArrayOf{GetGenericType(type).Name.ToLower()}");

						_arrayToBuild.Enqueue(type);
					}
					else
					{
						if (string.IsNullOrEmpty(name))
						{
							name = type.Name;
						}

						writer.WriteAttributeString("name", name);

						if (!isArray)
						{
							writer.WriteAttributeString("nillable", "true");
						}

						writer.WriteAttributeString("type", "tns:ArrayOf" + GetGenericType(type).Name);

						_complexTypeToBuild.Enqueue(type);
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
	}
}
