using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	public class MetaWCFBodyWriter : BodyWriter
	{
#pragma warning disable SA1310 // Field names must not contain underscore
		private const string XMLNS_XS = "http://www.w3.org/2001/XMLSchema";
		private const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";
		private const string ARRAYS_NS = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
		private const string SYSTEM_NS = "http://schemas.datacontract.org/2004/07/System";
		private const string DataContraceNamespace = "http://schemas.datacontract.org/2004/07/";
		private const string SERIALIZATION_NS = "http://schemas.microsoft.com/2003/10/Serialization/";
#pragma warning restore SA1310 // Field names must not contain underscore

#pragma warning disable SA1009 // Closing parenthesis must be spaced correctly
#pragma warning disable SA1008 // Opening parenthesis must be spaced correctly
		private static readonly Dictionary<string, (string, string)> SysTypeDic = new Dictionary<string, (string, string)>()
		{
			["System.String"] = ("string", SYSTEM_NS),
			["System.Boolean"] = ("boolean", SYSTEM_NS),
			["System.Int16"] = ("short", SYSTEM_NS),
			["System.Int32"] = ("int", SYSTEM_NS),
			["System.Int64"] = ("long", SYSTEM_NS),
			["System.Byte"] = ("byte", SYSTEM_NS),
			["System.SByte"] = ("byte", SYSTEM_NS),
			["System.UInt16"] = ("unsignedShort", SYSTEM_NS),
			["System.UInt32"] = ("unsignedInt", SYSTEM_NS),
			["System.UInt64"] = ("unsignedLong", SYSTEM_NS),
			["System.Decimal"] = ("decimal", SYSTEM_NS),
			["System.Double"] = ("double", SYSTEM_NS),
			["System.Single"] = ("float", SYSTEM_NS),
			["System.DateTime"] = ("dateTime", SYSTEM_NS),
			["System.Decimal"] = ("decimal", SYSTEM_NS),
			["System.Guid"] = ("guid", SERIALIZATION_NS),
			["System.Char"] = ("char", SERIALIZATION_NS),
			["System.TimeSpan"] = ("duration", SERIALIZATION_NS)
		};
#pragma warning restore SA1008 // Opening parenthesis must be spaced correctly
#pragma warning restore SA1009 // Closing parenthesis must be spaced correctly

		private static int _namespaceCounter = 1;

		private readonly ServiceDescription _service;
		private readonly string _baseUrl;
		private readonly Binding _binding;

		private readonly Queue<Type> _complexTypeToBuild;
		private readonly Queue<Type> _arrayToBuild;

		private readonly HashSet<string> _builtEnumTypes;
		private readonly HashSet<string> _builtComplexTypes;
		private readonly HashSet<string> _buildArrayTypes;

		private bool _buildDateTimeOffset;
		private string _schemaNamespace;

		public MetaWCFBodyWriter(ServiceDescription service, string baseUrl, Binding binding) : base(isBuffered: true)
		{
			_service = service;
			_baseUrl = baseUrl;
			_binding = binding;

			_complexTypeToBuild = new Queue<Type>();
			_arrayToBuild = new Queue<Type>();
			_builtEnumTypes = new HashSet<string>();
			_builtComplexTypes = new HashSet<string>();
			_buildArrayTypes = new HashSet<string>();

			BindingType = service.Contracts.First().Name;

			if (binding != null)
			{
				BindingName = $"{binding.Name}_{_service.Contracts.First().Name}";
				PortName = $"{binding.Name}_{_service.Contracts.First().Name}";
			}
			else
			{
				BindingName = "BasicHttpBinding_" + _service.Contracts.First().Name;
				PortName = "BasicHttpBinding_" + _service.Contracts.First().Name;
			}
		}

		private string BindingName { get; }
		private string BindingType { get; }
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

		private string GetModelNamespace(Type type)
		{
			if (type != null && type.Namespace != _service.ServiceType.Namespace)
			{
				return $"{DataContraceNamespace}{type.Namespace}";
			}

			return $"{DataContraceNamespace}{_service.ServiceType.Namespace}";
		}

		private string GetModelNamespace(string @namespace)
		{
			return $"{DataContraceNamespace}{@namespace}";
		}

		private void WriteParameters(XmlDictionaryWriter writer, SoapMethodParameterInfo[] parameterInfos)
		{
			foreach (var parameterInfo in parameterInfos)
			{
				var elementAttribute = parameterInfo.Parameter.GetCustomAttribute<XmlElementAttribute>();
				var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
										? elementAttribute.ElementName
										: parameterInfo.Parameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameterInfo.Parameter.Name;
				AddSchemaType(writer, parameterInfo.Parameter.ParameterType, parameterName, objectNamespace: elementAttribute?.Namespace);
			}
		}

		private void AddOperations(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("xs:schema");
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", TargetNameSpace);
			writer.WriteAttributeString("xmlns:xs", XMLNS_XS);

			_schemaNamespace = TargetNameSpace;
			_namespaceCounter = 1;

			//discovery all parameters types which namespaceses diff with service namespace
			var namespaces = _service.Operations.SelectMany(x => x.AllParameters.Where(parameter => parameter.Parameter.ParameterType.Namespace != _service.ServiceType.Namespace && parameter.Parameter.ParameterType.Namespace != "System")).Select(x => x.Parameter.ParameterType.Namespace);

			foreach (var @namespace in namespaces.Distinct())
			{
				writer.WriteStartElement("xs:import");
				writer.WriteAttributeString("namespace", DataContraceNamespace + @namespace);
				writer.WriteEndElement();
			}

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

			writer.WriteEndElement(); // xs:schema
		}

		private void AddTypes(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl:types");
			AddOperations(writer);
			AddMSSerialization(writer);
			AddComplexTypes(writer);
			AddArrayTypes(writer);
			AddSystemTypes(writer);
			writer.WriteEndElement(); // wsdl:types
		}

		private void AddSystemTypes(XmlDictionaryWriter writer)
		{
			if (_buildDateTimeOffset)
			{
				writer.WriteStartElement("xs:schema");
				writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
				writer.WriteAttributeString("xmlns:tns", SYSTEM_NS);
				writer.WriteAttributeString("elementFormDefault", "qualified");
				writer.WriteAttributeString("targetNamespace", SYSTEM_NS);

				writer.WriteStartElement("xs:import");
				writer.WriteAttributeString("namespace", SERIALIZATION_NS);
				writer.WriteEndElement();

				writer.WriteStartElement("xs:complexType");
				writer.WriteAttributeString("name", "DateTimeOffset");
				writer.WriteStartElement("xs:annotation");
				writer.WriteStartElement("xs:appinfo");

				writer.WriteElementString("IsValueType", SERIALIZATION_NS, "true");
				writer.WriteEndElement(); // xs:appinfo
				writer.WriteEndElement(); // xs:annotation

				writer.WriteStartElement("xs:sequence");

				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", "DateTime");
				writer.WriteAttributeString("type", "xs:dateTime");
				writer.WriteEndElement();

				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", "OffsetMinutes");
				writer.WriteAttributeString("type", "xs:short");
				writer.WriteEndElement();

				writer.WriteEndElement(); // xs:sequence

				writer.WriteEndElement(); // xs:complexType

				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", "DateTimeOffset");
				writer.WriteAttributeString("nillable", "true");
				writer.WriteAttributeString("type", "tns:DateTimeOffset");
				writer.WriteEndElement();

				writer.WriteEndElement(); // xs:schema
			}
		}

		private void AddArrayTypes(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("xs:schema");
			writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
			writer.WriteAttributeString("xmlns:tns", ARRAYS_NS);
			writer.WriteAttributeString("xmlns:ser", SERIALIZATION_NS);
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", ARRAYS_NS);
			_namespaceCounter = 1;
			_schemaNamespace = ARRAYS_NS;

			writer.WriteStartElement("xs:import");
			writer.WriteAttributeString("namespace", SERIALIZATION_NS);
			writer.WriteEndElement();

			while (_arrayToBuild.Count > 0)
			{
				var toBuild = _arrayToBuild.Dequeue();
				var elType = toBuild.IsArray ? toBuild.GetElementType() : GetGenericType(toBuild);
				var sysType = ResolveSystemType(elType);
				var toBuildName = "ArrayOf" + sysType.name;

				if (!_buildArrayTypes.Contains(toBuildName))
				{
					writer.WriteStartElement("xs:complexType");
					writer.WriteAttributeString("name", toBuildName);

					writer.WriteStartElement("xs:sequence");
					AddSchemaType(writer, elType, null, true);
					writer.WriteEndElement(); // :sequence

					writer.WriteEndElement(); // xs:complexType

					writer.WriteStartElement("xs:element");
					writer.WriteAttributeString("name", toBuildName);
					writer.WriteAttributeString("nillable", "true");
					writer.WriteAttributeString("type", "tns:" + toBuildName);
					writer.WriteEndElement(); // xs:element
					_buildArrayTypes.Add(toBuildName);
				}
			}

			writer.WriteEndElement(); // xs:schema
		}

		private void AddMSSerialization(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("xs:schema");
			writer.WriteAttributeString("attributeFormDefault", "qualified");
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", SERIALIZATION_NS);
			writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
			writer.WriteAttributeString("xmlns:tns", SERIALIZATION_NS);
			WriteSerializationElement(writer, "anyType", "xs:anyType", true);
			WriteSerializationElement(writer, "anyURI", "xs:anyURI", true);
			WriteSerializationElement(writer, "base64Binary", "xs:base64Binary", true);
			WriteSerializationElement(writer, "boolean", "xs:boolean", true);
			WriteSerializationElement(writer, "byte", "xs:byte", true);
			WriteSerializationElement(writer, "dateTime", "xs:dateTime", true);
			WriteSerializationElement(writer, "decimal", "xs:decimal", true);
			WriteSerializationElement(writer, "double", "xs:double", true);
			WriteSerializationElement(writer, "float", "xs:float", true);
			WriteSerializationElement(writer, "int", "xs:int", true);
			WriteSerializationElement(writer, "long", "xs:long", true);
			WriteSerializationElement(writer, "QName", "xs:QName", true);
			WriteSerializationElement(writer, "short", "xs:short", true);
			WriteSerializationElement(writer, "string", "xs:string", true);
			WriteSerializationElement(writer, "unsignedByte", "xs:unsignedByte", true);
			WriteSerializationElement(writer, "unsignedInt", "xs:unsignedInt", true);
			WriteSerializationElement(writer, "unsignedLong", "xs:unsignedLong", true);
			WriteSerializationElement(writer, "unsignedShort", "xs:unsignedShort", true);

			WriteSerializationElement(writer, "char", "tns:char", true);
			writer.WriteStartElement("xs:simpleType");
			writer.WriteAttributeString("name", "char");
			writer.WriteStartElement("xs:restriction");
			writer.WriteAttributeString("base", "xs:int");
			writer.WriteEndElement();
			writer.WriteEndElement();

			WriteSerializationElement(writer, "duration", "tns:duration", true);
			writer.WriteStartElement("xs:simpleType");
			writer.WriteAttributeString("name", "duration");
			writer.WriteStartElement("xs:restriction");
			writer.WriteAttributeString("base", "xs:duration");
			writer.WriteStartElement("xs:pattern");
			writer.WriteAttributeString("value", @"\-?P(\d*D)?(T(\d*H)?(\d*M)?(\d*(\.\d*)?S)?)?");
			writer.WriteEndElement();
			writer.WriteStartElement("xs:minInclusive");
			writer.WriteAttributeString("value", @"-P10675199DT2H48M5.4775808S");
			writer.WriteEndElement();
			writer.WriteStartElement("xs:maxInclusive");
			writer.WriteAttributeString("value", @"P10675199DT2H48M5.4775807S");
			writer.WriteEndElement();
			writer.WriteEndElement();
			writer.WriteEndElement();

			WriteSerializationElement(writer, "guid", "tns:guid", true);
			writer.WriteStartElement("xs:simpleType");
			writer.WriteAttributeString("name", "guid");
			writer.WriteStartElement("xs:restriction");
			writer.WriteAttributeString("base", "xs:string");
			writer.WriteStartElement("xs:pattern");
			writer.WriteAttributeString("value", @"[\da-fA-F]{8}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{12}");
			writer.WriteEndElement();
			writer.WriteEndElement();
			writer.WriteEndElement();

			writer.WriteStartElement("xs:attribute");
			writer.WriteAttributeString("name", "FactoryType");
			writer.WriteAttributeString("type", "xs:QName");
			writer.WriteEndElement();

			writer.WriteStartElement("xs:attribute");
			writer.WriteAttributeString("name", "Id");
			writer.WriteAttributeString("type", "xs:ID");
			writer.WriteEndElement();

			writer.WriteStartElement("xs:attribute");
			writer.WriteAttributeString("name", "Ref");
			writer.WriteAttributeString("type", "xs:IDREF");
			writer.WriteEndElement();

			writer.WriteEndElement(); //schema
		}

		private void WriteSerializationElement(XmlDictionaryWriter writer, string name, string type, bool nillable)
		{
			writer.WriteStartElement("xs:element");
			writer.WriteAttributeString("name", name);
			writer.WriteAttributeString("nillable", nillable ? "true" : "false");
			writer.WriteAttributeString("type", type);
			writer.WriteEndElement();
		}

		private void AddComplexTypes(XmlDictionaryWriter writer)
		{
			foreach (var type in _complexTypeToBuild.ToArray())
			{
				DiscoveryTypesByProperties(type, true);
			}

			var groupedByNamespace = _complexTypeToBuild.GroupBy(x => x.Namespace);

			foreach (var types in groupedByNamespace.Distinct())
			{
				writer.WriteStartElement("xs:schema");
				writer.WriteAttributeString("elementFormDefault", "qualified");
				writer.WriteAttributeString("targetNamespace", GetModelNamespace(types.Key));
				writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
				writer.WriteAttributeString("xmlns:tns", GetModelNamespace(types.Key));
				writer.WriteAttributeString("xmlns:ser", SERIALIZATION_NS);

				_namespaceCounter = 1;
				_schemaNamespace = GetModelNamespace(types.Key);

				writer.WriteStartElement("xs:import");
				writer.WriteAttributeString("namespace", SYSTEM_NS);
				writer.WriteEndElement();

				writer.WriteStartElement("xs:import");
				writer.WriteAttributeString("namespace", ARRAYS_NS);
				writer.WriteEndElement();

				foreach (var type in types.Distinct())
				{
					if (type.IsEnum)
					{
						WriteEnum(writer, type);
					}
					else
					{
						WriteComplexType(writer, type);
					}

					writer.WriteStartElement("xs:element");
					writer.WriteAttributeString("name", GetTypeName(type));
					writer.WriteAttributeString("nillable", "true");
					writer.WriteAttributeString("type", "tns:" + GetTypeName(type));
					writer.WriteEndElement(); // xs:element
				}

				writer.WriteEndElement();
			}
		}

		private void DiscoveryTypesByProperties(Type type, bool isRootType)
		{
			//guard against infinity recursion
			if (!isRootType && _complexTypeToBuild.Contains(type))
			{
				return;
			}

			if (HasBaseType(type))
			{
				DiscoveryTypesByProperties(type.BaseType, false);
				_complexTypeToBuild.Enqueue(type.BaseType);
			}

			foreach (var property in type.GetProperties().Where(prop =>
						prop.DeclaringType == type
						&& prop.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute")
				        && !prop.PropertyType.IsPrimitive && !SysTypeDic.ContainsKey(prop.PropertyType.FullName)))
			{
				Type propertyType;
				var underlyingType = Nullable.GetUnderlyingType(property.PropertyType);
				if (Nullable.GetUnderlyingType(property.PropertyType) != null)
				{
					propertyType = underlyingType;
				}
				else if (property.PropertyType.IsArray || typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
				{
					propertyType = property.PropertyType.IsArray
						? property.PropertyType.GetElementType()
						: GetGenericType(property.PropertyType);
					_complexTypeToBuild.Enqueue(property.PropertyType);
				}
				else
				{
					propertyType = property.PropertyType;
				}

				if (!propertyType.IsPrimitive && !SysTypeDic.ContainsKey(propertyType.FullName))
				{
					DiscoveryTypesByProperties(propertyType, false);
					_complexTypeToBuild.Enqueue(propertyType);
				}
			}
		}

		private void WriteEnum(XmlDictionaryWriter writer, Type type)
		{
			if (type.IsByRef)
			{
				type = type.GetElementType();
			}

			if (!_builtEnumTypes.Contains(type.Name))
			{
				writer.WriteStartElement("xs:simpleType");
				writer.WriteAttributeString("name", type.Name);
				writer.WriteStartElement("xs:restriction ");
				writer.WriteAttributeString("base", "xs:string");

				foreach (var value in Enum.GetValues(type))
				{
					writer.WriteStartElement("xs:enumeration ");
					writer.WriteAttributeString("value", value.ToString());
					writer.WriteEndElement(); // xs:enumeration
				}

				writer.WriteEndElement(); // xs:restriction
				writer.WriteEndElement(); // xs:simpleType

				_builtEnumTypes.Add(type.Name);
			}
		}

		private void WriteComplexType(XmlDictionaryWriter writer, Type type)
		{
			var toBuildName = GetTypeName(type);

			if (!_builtComplexTypes.Contains(toBuildName))
			{
				writer.WriteStartElement("xs:complexType");
				writer.WriteAttributeString("name", toBuildName);
				writer.WriteAttributeString("xmlns:ser", SERIALIZATION_NS);

				var hasBaseType = HasBaseType(type);

				if (hasBaseType)
				{
					writer.WriteStartElement("xs:complexContent");

					writer.WriteStartElement("xs:extension");
					writer.WriteAttributeString("base", $"tns:{type.BaseType.Name}");
				}

				writer.WriteStartElement("xs:sequence");

				if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
				{
					var elementType = type.IsArray ? type.GetElementType() : GetGenericType(type);
					AddSchemaType(writer, elementType, null, true);
				}
				else
				{
					var properties = type.GetProperties().Where(prop =>
						prop.DeclaringType == type &&
						prop.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute"));

					//TODO: base type properties
					//TODO: enforce order attribute parameters
					foreach (var property in properties.OrderBy(p => p.Name))
					{
						AddSchemaType(writer, property.PropertyType, property.Name);
					}
				}

				writer.WriteEndElement(); // xs:sequence

				if (hasBaseType)
				{
					writer.WriteEndElement(); // xs:extension
					writer.WriteEndElement(); // xs:complexContent
				}

				writer.WriteEndElement(); // xs:complexType

				_builtComplexTypes.Add(toBuildName);
			}
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
				writer.WriteAttributeString("wsam:Action", operation.SoapAction);
				writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_InputMessage");
				writer.WriteEndElement(); // wsdl:input
				writer.WriteStartElement("wsdl:output");
				writer.WriteAttributeString("wsam:Action", operation.SoapAction + "Response");
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

			if (_binding.HasBasicAuth())
			{
				writer.WriteStartElement("wsp:PolicyReference");
				writer.WriteAttributeString("URI", $"#{_binding.Name}_{_service.Contracts.First().Name}_policy");
				writer.WriteEndElement();
			}

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

		private void AddSchemaType(XmlDictionaryWriter writer, Type type, string name, bool isArray = false, string objectNamespace = null)
		{
			var typeInfo = type.GetTypeInfo();
			if (typeInfo.IsByRef)
			{
				type = typeInfo.GetElementType();
			}

			writer.WriteStartElement("xs:element");

			if (objectNamespace == null)
			{
				objectNamespace = GetModelNamespace(type);
			}

			if (typeInfo.IsEnum)
			{
				WriteComplexElementType(writer, type.Name, _schemaNamespace, objectNamespace, type);
				writer.WriteAttributeString("name", type.Name);
			}
			else if (typeInfo.IsValueType)
			{
				string xsTypename;
				if (typeof(DateTimeOffset).IsAssignableFrom(type))
				{
					if (string.IsNullOrEmpty(name))
					{
						name = type.Name;
					}

					var ns = $"q{_namespaceCounter++}";
					xsTypename = $"{ns}:{type.Name}";
					writer.WriteAttributeString($"xmlns:{ns}", SYSTEM_NS);

					_buildDateTimeOffset = true;
				}
				else
				{
					var underlyingType = Nullable.GetUnderlyingType(type);
					if (underlyingType != null)
					{
						var sysType = ResolveSystemType(underlyingType);
						xsTypename = $"{(sysType.ns == SERIALIZATION_NS ? "ser" : "xs")}:{sysType.name}";
						writer.WriteAttributeString("nillable", "true");
					}
					else
					{
						var sysType = ResolveSystemType(type);
						xsTypename = $"{(sysType.ns == SERIALIZATION_NS ? "ser" : "xs")}:{sysType.name}";
					}
				}

				writer.WriteAttributeString("minOccurs", "0");
				if (isArray)
				{
					writer.WriteAttributeString("maxOccurs", "unbounded");
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
				}

				if (type.Name == "String" || type.Name == "String&")
				{
					if (string.IsNullOrEmpty(name))
					{
						name = "string";
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("nillable", "true");
					writer.WriteAttributeString("type", "xs:string");
				}
				else if (type == typeof(System.Xml.Linq.XElement))
				{
					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("nillable", "true");
					writer.WriteStartElement("xs:complexType");
					writer.WriteStartElement("xs:sequence");
					writer.WriteStartElement("xs:any");
					writer.WriteAttributeString("minOccurs", "0");
					writer.WriteAttributeString("processContents", "lax");
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
				else if (typeof(IEnumerable).IsAssignableFrom(type))
				{
					var elType = type.IsArray ? type.GetElementType() : GetGenericType(type);
					var sysType = ResolveSystemType(elType);
					if (sysType.name != null)
					{
						if (string.IsNullOrEmpty(name))
						{
							name = type.Name;
						}

						var ns = $"q{_namespaceCounter++}";

						writer.WriteAttributeString($"xmlns:{ns}", ARRAYS_NS);
						writer.WriteAttributeString("name", name);
						writer.WriteAttributeString("nillable", "true");
						writer.WriteAttributeString("type", $"{ns}:ArrayOf{sysType.name}");

						_arrayToBuild.Enqueue(type);
					}
					else
					{
						if (string.IsNullOrEmpty(name))
						{
							name = type.Name;
						}

						writer.WriteAttributeString("name", name);
						WriteComplexElementType(writer, $"ArrayOf{elType.Name}", _schemaNamespace, objectNamespace, type);
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
					WriteComplexElementType(writer, type.Name, _schemaNamespace, objectNamespace, type);
					_complexTypeToBuild.Enqueue(type);
				}
			}

			writer.WriteEndElement(); // xs:element
		}

		private void WriteComplexElementType(XmlDictionaryWriter writer, string typeName, string schemaNamespace, string objectNamespace, Type type)
		{
			writer.WriteAttributeString("nillable", "true");
			if (schemaNamespace != objectNamespace)
			{
				var ns = $"q{_namespaceCounter++}";
				writer.WriteAttributeString("type", $"{ns}:{typeName}");
				writer.WriteAttributeString($"xmlns:{ns}", GetModelNamespace(type));
			}
			else
			{
				writer.WriteAttributeString("type", $"tns:{typeName}");
			}
		}

		private string GetTypeName(Type type)
		{
			return type.IsArray ? "ArrayOf" + type.GetElementType().Name : typeof(IEnumerable).IsAssignableFrom(type) ? "ArrayOf" + GetGenericType(type).Name : type.Name;
		}

#pragma warning disable SA1009 // Closing parenthesis must be spaced correctly
#pragma warning disable SA1008 // Opening parenthesis must be spaced correctly
		private (string name, string ns) ResolveSystemType(Type type)
		{
			type = type.IsEnum ? type.GetEnumUnderlyingType() : type;
			if (SysTypeDic.ContainsKey(type.FullName))
			{
				return SysTypeDic[type.FullName];
			}

			return (null, null);
		}
#pragma warning restore SA1008 // Opening parenthesis must be spaced correctly
#pragma warning restore SA1009 // Closing parenthesis must be spaced correctly

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

		private bool HasBaseType(Type type)
		{
			var isArrayType = type.IsArray || typeof(IEnumerable).IsAssignableFrom(type);

			var baseType = type.GetTypeInfo().BaseType;

			return !isArrayType && !type.IsEnum && !type.IsPrimitive && !baseType.Name.Equals("Object");
		}
	}
}
