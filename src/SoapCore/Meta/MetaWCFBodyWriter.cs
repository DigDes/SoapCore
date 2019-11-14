using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
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
	internal class MetaWCFBodyWriter : BodyWriter
	{
#pragma warning disable SA1310 // Field names must not contain underscore
		private const string XMLNS_XS = "http://www.w3.org/2001/XMLSchema";
		private const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";
		private const string ARRAYS_NS = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
		private const string SYSTEM_NS = "http://schemas.datacontract.org/2004/07/System";
		private const string DataContractNamespace = "http://schemas.datacontract.org/2004/07/";
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
			["System.Guid"] = ("guid", SERIALIZATION_NS),
			["System.Char"] = ("char", SERIALIZATION_NS),
			["System.TimeSpan"] = ("duration", SERIALIZATION_NS),
			["System.Object"] = ("anyType", SERIALIZATION_NS)
		};
#pragma warning restore SA1008 // Opening parenthesis must be spaced correctly
#pragma warning restore SA1009 // Closing parenthesis must be spaced correctly

		private static int _namespaceCounter = 1;

		private readonly ServiceDescription _service;
		private readonly string _baseUrl;
		private readonly Binding _binding;

		private readonly Dictionary<Type, string> _complexTypeToBuild = new Dictionary<Type, string>();
		private readonly HashSet<Type> _complexTypeProcessed = new HashSet<Type>(); // Contains types that have been discovered
		private readonly Queue<Type> _arrayToBuild;

		private readonly HashSet<string> _builtEnumTypes;
		private readonly HashSet<string> _builtComplexTypes;
		private readonly HashSet<string> _buildArrayTypes;
		private readonly HashSet<string> _builtSerializationElements;

		private bool _buildDateTimeOffset;
		private bool _buildDataTable;
		private string _schemaNamespace;

		public MetaWCFBodyWriter(ServiceDescription service, string baseUrl, Binding binding) : base(isBuffered: true)
		{
			_service = service;
			_baseUrl = baseUrl;
			_binding = binding;

			_arrayToBuild = new Queue<Type>();
			_builtEnumTypes = new HashSet<string>();
			_builtComplexTypes = new HashSet<string>();
			_buildArrayTypes = new HashSet<string>();
			_builtSerializationElements = new HashSet<string>();

			BindingType = service.Contracts.First().Name;

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

		private static string GetModelNamespace(string @namespace)
		{
			if (@namespace.StartsWith("http"))
			{
				return @namespace;
			}

			return $"{DataContractNamespace}{@namespace}";
		}

		private static string GetDataContractNamespace(Type type)
		{
			if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
			{
				type = type.IsArray ? type.GetElementType() : GetGenericType(type);
			}

			var dataContractAttribute = type.GetCustomAttribute<DataContractAttribute>();
			if (dataContractAttribute != null && !string.IsNullOrEmpty(dataContractAttribute.Namespace))
			{
				return dataContractAttribute.Namespace;
			}

			return GetModelNamespace(type.Namespace);
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

		private string GetModelNamespace(Type type)
		{
			if (type != null && type.Namespace != _service.ServiceType.Namespace)
			{
				return $"{DataContractNamespace}{type.Namespace}";
			}

			return $"{DataContractNamespace}{_service.ServiceType.Namespace}";
		}

		private void WriteParameters(XmlDictionaryWriter writer, SoapMethodParameterInfo[] parameterInfos)
		{
			foreach (var parameterInfo in parameterInfos)
			{
				var elementAttribute = parameterInfo.Parameter.GetCustomAttribute<XmlElementAttribute>();
				var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
										? elementAttribute.ElementName
										: parameterInfo.Parameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameterInfo.Parameter.Name;
				AddSchemaType(writer, parameterInfo.Parameter.ParameterType, parameterName, objectNamespace: elementAttribute?.Namespace ?? (parameterInfo.Namespace != "http://tempuri.org/" ? parameterInfo.Namespace : null));
			}
		}

		private void AddOperations(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("xs:schema");
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", TargetNameSpace);
			writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
			writer.WriteAttributeString("xmlns:ser", SERIALIZATION_NS);

			_schemaNamespace = TargetNameSpace;
			_namespaceCounter = 1;

			//discovery all parameters types which namespaceses diff with service namespace
			foreach (var operation in _service.Operations)
			{
				foreach (var parameter in operation.AllParameters)
				{
					var type = parameter.Parameter.ParameterType;
					var typeInfo = type.GetTypeInfo();
					if (typeInfo.IsByRef)
					{
						type = typeInfo.GetElementType();
					}

					if (TypeIsComplexForWsdl(type, out type))
					{
						_complexTypeToBuild[type] = GetDataContractNamespace(type);
						DiscoveryTypesByProperties(type, true);
					}
					else if (type.IsEnum || Nullable.GetUnderlyingType(type)?.IsEnum == true)
					{
						_complexTypeToBuild[type] = GetDataContractNamespace(type);
						DiscoveryTypesByProperties(type, true);
					}
				}

				if (operation.DispatchMethod.ReturnType != typeof(void) && operation.DispatchMethod.ReturnType != typeof(Task))
				{
					var returnType = operation.DispatchMethod.ReturnType;
					if (returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					{
						returnType = returnType.GetGenericArguments().First();
					}

					if (TypeIsComplexForWsdl(returnType, out returnType))
					{
						_complexTypeToBuild[returnType] = GetDataContractNamespace(returnType);
						DiscoveryTypesByProperties(returnType, true);
					}
					else if (returnType.IsEnum || Nullable.GetUnderlyingType(returnType)?.IsEnum == true)
					{
						_complexTypeToBuild[returnType] = GetDataContractNamespace(returnType);
						DiscoveryTypesByProperties(returnType, true);
					}
				}
			}

			var groupedByNamespace = _complexTypeToBuild.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.Select(k => k.Key));

			foreach (var @namespace in groupedByNamespace.Keys.Where(x => x != null && x != _service.ServiceType.Namespace).Distinct())
			{
				writer.WriteStartElement("xs:import");
				writer.WriteAttributeString("namespace", @namespace);
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

				if (operation.DispatchMethod.ReturnType != typeof(void) && operation.DispatchMethod.ReturnType != typeof(Task))
				{
					var returnType = operation.DispatchMethod.ReturnType;
					if (returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					{
						returnType = returnType.GetGenericArguments().First();
					}

					var returnName = operation.DispatchMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? operation.Name + "Result";
					AddSchemaType(writer, returnType, returnName, false, GetDataContractNamespace(returnType));
				}

				WriteParameters(writer, operation.OutParameters);

				writer.WriteEndElement(); // xs:sequence
				writer.WriteEndElement(); // xs:complexType
				writer.WriteEndElement(); // xs:element

				AddFaultTypes(writer, operation);
			}

			writer.WriteEndElement(); // xs:schema
		}

		private void AddFaultTypes(XmlDictionaryWriter writer, OperationDescription operation)
		{
			foreach (var faultType in operation.Faults)
			{
				if (_complexTypeProcessed.Contains(faultType))
				{
					continue;
				}

				_complexTypeToBuild[faultType] = GetDataContractNamespace(faultType);
				DiscoveryTypesByProperties(faultType, true);
			}
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

			if (_buildDataTable)
			{
				writer.WriteStartElement("xs:schema");
				writer.WriteAttributeString("elementFormDefault", "qualified");
				writer.WriteAttributeString("targetNamespace", "http://schemas.datacontract.org/2004/07/System.Data");
				writer.WriteAttributeString("xmlns:xs", "http://www.w3.org/2001/XMLSchema");
				writer.WriteAttributeString("xmlns:tns", "http://schemas.datacontract.org/2004/07/System.Data");

				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", "DataTable");
				writer.WriteAttributeString("nillable", "true");

				writer.WriteStartElement("xs:complexType");
				writer.WriteStartElement("xs:annotation");

				writer.WriteStartElement("xs:appinfo");
				writer.WriteStartElement("ActualType");
				writer.WriteAttributeString("xmlns", "http://schemas.microsoft.com/2003/10/Serialization/");
				writer.WriteAttributeString("Name", "DataTable");
				writer.WriteAttributeString("Namespace", "http://schemas.datacontract.org/2004/07/System.Data");
				writer.WriteEndElement(); //actual type
				writer.WriteEndElement(); //appinfo
				writer.WriteEndElement(); //annotation

				writer.WriteStartElement("xs:sequence");

				writer.WriteStartElement("xs:any");
				writer.WriteAttributeString("minOccurs", "0");
				writer.WriteAttributeString("maxOccurs", "unbounded");
				writer.WriteAttributeString("namespace", "http://www.w3.org/2001/XMLSchema");
				writer.WriteAttributeString("processContents", "lax");
				writer.WriteEndElement(); //any

				writer.WriteStartElement("xs:any");
				writer.WriteAttributeString("minOccurs", "1");
				writer.WriteAttributeString("namespace", "urn:schemas-microsoft-com:xml-diffgram-v1");
				writer.WriteAttributeString("processContents", "lax");
				writer.WriteEndElement(); //any

				writer.WriteEndElement(); //sequence

				writer.WriteEndElement();  //complexType

				writer.WriteEndElement(); //element

				writer.WriteEndElement(); //schema
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
			if (!_builtSerializationElements.Contains(name))
			{
				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", name);
				writer.WriteAttributeString("nillable", nillable ? "true" : "false");
				writer.WriteAttributeString("type", type);
				writer.WriteEndElement();

				_builtSerializationElements.Add(name);
			}
		}

		private void AddComplexTypes(XmlDictionaryWriter writer)
		{
			foreach (var type in _complexTypeToBuild.ToArray())
			{
				_complexTypeToBuild[type.Key] = GetDataContractNamespace(type.Key);
				DiscoveryTypesByProperties(type.Key, true);
			}

			var groupedByNamespace = _complexTypeToBuild.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.Select(k => k.Key));

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

				foreach (var type in types.Value.Distinct(new TypesComparer(GetTypeName)))
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
					if (!type.IsEnum || Nullable.GetUnderlyingType(type) != null)
					{
						writer.WriteAttributeString("nillable", "true");
					}

					writer.WriteAttributeString("type", "tns:" + GetTypeName(type));
					writer.WriteEndElement(); // xs:element
				}

				writer.WriteEndElement();
			}
		}

		private void DiscoveryTypesByProperties(Type type, bool isRootType)
		{
			//guard against infinity recursion
			//check is made against _complexTypeProcessed, which contains types that have been
			//discovered by the current method
			if (_complexTypeProcessed.Contains(type))
			{
				return;
			}

			if (type == typeof(DateTimeOffset))
			{
				return;
			}

			//type will be processed, so can be added to _complexTypeProcessed
			_complexTypeProcessed.Add(type);

			if (HasBaseType(type) && type.BaseType != null)
			{
				_complexTypeToBuild[type.BaseType] = GetDataContractNamespace(type.BaseType);
				DiscoveryTypesByProperties(type.BaseType, false);
			}

			foreach (var property in type.GetProperties().Where(prop =>
						prop.DeclaringType == type
						&& prop.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute")
						&& !prop.PropertyType.IsPrimitive
						&& !SysTypeDic.ContainsKey(prop.PropertyType.FullName)
						&& prop.PropertyType != typeof(ValueType)
						&& prop.PropertyType != typeof(DateTimeOffset)))
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
					_complexTypeToBuild[property.PropertyType] = GetDataContractNamespace(property.PropertyType);
				}
				else
				{
					propertyType = property.PropertyType;
				}

				if (propertyType != null && !propertyType.IsPrimitive && !SysTypeDic.ContainsKey(propertyType.FullName))
				{
					if (propertyType == type)
					{
						continue;
					}

					_complexTypeToBuild[propertyType] = GetDataContractNamespace(propertyType);
					DiscoveryTypesByProperties(propertyType, false);
				}
			}
		}

		private void WriteEnum(XmlDictionaryWriter writer, Type type)
		{
			if (type.IsByRef)
			{
				type = type.GetElementType();
			}

			var typeName = GetTypeName(type);

			if (!_builtEnumTypes.Contains(typeName))
			{
				writer.WriteStartElement("xs:simpleType");
				writer.WriteAttributeString("name", typeName);
				writer.WriteStartElement("xs:restriction ");
				writer.WriteAttributeString("base", "xs:string");

				foreach (var name in Enum.GetNames(type))
				{
					writer.WriteStartElement("xs:enumeration ");

					// Search for EnumMember attribute. If available, get enum value from its Value field
					var enumMemberAttribute = ((EnumMemberAttribute[])type.GetField(name).GetCustomAttributes(typeof(EnumMemberAttribute), true)).SingleOrDefault();
					var value = enumMemberAttribute is null || !enumMemberAttribute.IsValueSetExplicitly
						? name
						: enumMemberAttribute.Value;

					writer.WriteAttributeString("value", value);
					writer.WriteEndElement(); // xs:enumeration
				}

				writer.WriteEndElement(); // xs:restriction
				writer.WriteEndElement(); // xs:simpleType

				_builtEnumTypes.Add(typeName);
			}
		}

		private void WriteComplexType(XmlDictionaryWriter writer, Type type)
		{
			var toBuildName = GetTypeName(type);

			if (_builtComplexTypes.Contains(toBuildName))
			{
				return;
			}

			writer.WriteStartElement("xs:complexType");
			writer.WriteAttributeString("name", toBuildName);
			writer.WriteAttributeString("xmlns:ser", SERIALIZATION_NS);

			var hasBaseType = HasBaseType(type);

			if (hasBaseType)
			{
				writer.WriteStartElement("xs:complexContent");

				writer.WriteAttributeString("mixed", "false");

				writer.WriteStartElement("xs:extension");

				var modelNamespace = GetDataContractNamespace(type.BaseType);

				var typeName = type.BaseType.Name;

				if (_schemaNamespace != modelNamespace)
				{
					var ns = $"q{_namespaceCounter++}";
					writer.WriteAttributeString("base", $"{ns}:{typeName}");
					writer.WriteAttributeString($"xmlns:{ns}", modelNamespace);
				}
				else
				{
					writer.WriteAttributeString("base", $"tns:{typeName}");
				}
			}

			writer.WriteStartElement("xs:sequence");

			if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
			{
				var elementType = type.IsArray ? type.GetElementType() : GetGenericType(type);
				AddSchemaType(writer, elementType, null, true, GetDataContractNamespace(type));
			}
			else
			{
				var properties = type.GetProperties().Where(prop =>
					prop.DeclaringType == type &&
					prop.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute"));

				var dataMembersToWrite = new List<DataMemberDescription>();

				//TODO: base type properties
				//TODO: enforce order attribute parameters
				foreach (var property in properties)
				{
					var propertyName = property.Name;

					var attributes = property.GetCustomAttributes(true);
					int order = 0;
					foreach (var attr in attributes)
					{
						if (attr is DataMemberAttribute dataContractAttribute)
						{
							if (!string.IsNullOrEmpty(dataContractAttribute.Name))
							{
								propertyName = dataContractAttribute.Name;
							}

							if (dataContractAttribute.Order > 0)
							{
								order = dataContractAttribute.Order;
							}

							break;
						}
					}

					dataMembersToWrite.Add(new DataMemberDescription
					{
						Name = propertyName,
						Type = property.PropertyType,
						Order = order
					});
				}

				foreach (var p in dataMembersToWrite.OrderBy(x => x.Order).ThenBy(p => p.Name, StringComparer.Ordinal))
				{
					AddSchemaType(writer, p.Type, p.Name, false, GetDataContractNamespace(p.Type));
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

				AddMessageFaults(writer, operation);
			}
		}

		private void AddMessageFaults(XmlDictionaryWriter writer, OperationDescription operation)
		{
			foreach (Type fault in operation.Faults)
			{
				writer.WriteStartElement("wsdl:message");
				writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_{fault.Name}Fault_FaultMessage");
				writer.WriteStartElement("wsdl:part");
				writer.WriteAttributeString("name", "detail");
				var ns = $"q{_namespaceCounter++}";
				writer.WriteAttributeString("element", $"{ns}:{fault.Name}");
				writer.WriteAttributeString($"xmlns:{ns}", GetDataContractNamespace(fault));
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

				AddPortTypeFaults(writer, operation);

				writer.WriteEndElement(); // wsdl:operation
			}

			writer.WriteEndElement(); // wsdl:portType
		}

		private void AddPortTypeFaults(XmlDictionaryWriter writer, OperationDescription operation)
		{
			foreach (Type fault in operation.Faults)
			{
				writer.WriteStartElement("wsdl:fault");
				writer.WriteAttributeString("wsam:Action", $"{operation.SoapAction}{fault.Name}Fault");
				writer.WriteAttributeString("name", $"{fault.Name}Fault");
				writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_{fault.Name}Fault_FaultMessage");
				writer.WriteEndElement(); // wsdl:fault
			}
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

				AddBindingFaults(writer, operation);

				writer.WriteEndElement(); // wsdl:operation
			}

			writer.WriteEndElement(); // wsdl:binding
		}

		private void AddBindingFaults(XmlDictionaryWriter writer, OperationDescription operation)
		{
			foreach (Type fault in operation.Faults)
			{
				writer.WriteStartElement("wsdl:fault");
				writer.WriteAttributeString("name", $"{fault.Name}Fault");

				writer.WriteStartElement("soap:fault");
				writer.WriteAttributeString("use", "literal");
				writer.WriteAttributeString("name", $"{fault.Name}Fault");
				writer.WriteEndElement(); // soap:fault

				writer.WriteEndElement(); // wsdl:fault
			}
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
			var typeName = GetTypeName(type);

			if (typeInfo.IsByRef)
			{
				type = typeInfo.GetElementType();
			}

			writer.WriteStartElement("xs:element");

			if (objectNamespace == null)
			{
				objectNamespace = GetModelNamespace(type);
			}

			if (typeInfo.IsEnum || Nullable.GetUnderlyingType(typeInfo)?.IsEnum == true)
			{
				WriteComplexElementType(writer, typeName, _schemaNamespace, objectNamespace, type);

				if (string.IsNullOrEmpty(name))
				{
					name = typeName;
				}

				writer.WriteAttributeString("name", name);
			}
			else if (type.IsValueType)
			{
				string xsTypename;
				if (typeof(DateTimeOffset).IsAssignableFrom(type))
				{
					if (string.IsNullOrEmpty(name))
					{
						name = typeName;
					}

					var ns = $"q{_namespaceCounter++}";
					xsTypename = $"{ns}:{typeName}";
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
				else if (type.Name == "Object" || type.Name == "Object&")
				{
					writer.WriteAttributeString("name", "anyType");
					writer.WriteAttributeString("type", "xs:anyType");
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
				else if (type == typeof(DataTable))
				{
					_buildDataTable = true;

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("nillable", "true");
					writer.WriteStartElement("xs:complexType");
					writer.WriteStartElement("xs:annotation");
					writer.WriteStartElement("xs:appinfo");
					writer.WriteStartElement("ActualType");
					writer.WriteAttributeString("xmlns", "http://schemas.microsoft.com/2003/10/Serialization/");
					writer.WriteAttributeString("Name", "DataTable");
					writer.WriteAttributeString("Namespace", "http://schemas.datacontract.org/2004/07/System.Data");
					writer.WriteEndElement(); //actual type
					writer.WriteEndElement(); // appinfo
					writer.WriteEndElement(); //annotation
					writer.WriteEndElement(); //complex type

					writer.WriteStartElement("xs:sequence");

					writer.WriteStartElement("xs:any");
					writer.WriteAttributeString("minOccurs", "0");
					writer.WriteAttributeString("maxOccurs", "unbounded");
					writer.WriteAttributeString("namespace", "http://www.w3.org/2001/XMLSchema");
					writer.WriteAttributeString("processContents", "lax");
					writer.WriteEndElement();

					writer.WriteStartElement("xs:any");
					writer.WriteAttributeString("minOccurs", "1");
					writer.WriteAttributeString("namespace", "urn:schemas-microsoft-com:xml-diffgram-v1");
					writer.WriteAttributeString("processContents", "lax");
					writer.WriteEndElement();

					writer.WriteEndElement(); //sequence
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
				else if (typeof(IEnumerable).IsAssignableFrom(type))
				{
					var elType = type.IsArray ? type.GetElementType() : GetGenericType(type);
					var sysType = ResolveSystemType(elType);
					if (sysType.name != null)
					{
						if (string.IsNullOrEmpty(name))
						{
							name = typeName;
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
							name = typeName;
						}

						writer.WriteAttributeString("name", name);
						WriteComplexElementType(writer, typeName, _schemaNamespace, objectNamespace, type);
						_complexTypeToBuild[type] = GetDataContractNamespace(type);
					}
				}
				else
				{
					if (string.IsNullOrEmpty(name))
					{
						name = typeName;
					}

					writer.WriteAttributeString("name", name);
					WriteComplexElementType(writer, typeName, _schemaNamespace, objectNamespace, type);
					_complexTypeToBuild[type] = GetDataContractNamespace(type);
				}
			}

			writer.WriteEndElement(); // xs:element
		}

		private bool TypeIsComplexForWsdl(Type type, out Type resultType)
		{
			var typeInfo = type.GetTypeInfo();
			resultType = null;
			resultType = type;
			if (typeInfo.IsByRef)
			{
				type = typeInfo.GetElementType();
			}

			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				resultType = type.IsArray ? type.GetElementType() : GetGenericType(type);
				type = resultType;
			}

			if (typeInfo.IsEnum || typeInfo.IsValueType)
			{
				return false;
			}

			if (type.Name == "String" || type.Name == "String&")
			{
				return false;
			}

			if (type == typeof(System.Xml.Linq.XElement))
			{
				return false;
			}

			if (type == typeof(DataTable))
			{
				return false;
			}

			if (type.Name == "Byte[]")
			{
				return false;
			}

			if (SysTypeDic.ContainsKey(type.FullName))
			{
				return false;
			}

			return true;
		}

		private void WriteComplexElementType(XmlDictionaryWriter writer, string typeName, string schemaNamespace, string objectNamespace, Type type)
		{
			var underlying = Nullable.GetUnderlyingType(type);
			if (!type.IsEnum || underlying != null)
			{
				writer.WriteAttributeString("nillable", "true");
			}

			// In case of Nullable<T>, type is replaced by the underlying type
			if (underlying?.IsEnum == true)
			{
				type = underlying;
				typeName = GetTypeName(underlying);
				objectNamespace = GetModelNamespace(underlying);
			}

			if (schemaNamespace != objectNamespace)
			{
				var ns = $"q{_namespaceCounter++}";
				writer.WriteAttributeString("type", $"{ns}:{typeName}");
				writer.WriteAttributeString($"xmlns:{ns}", GetDataContractNamespace(type));
			}
			else
			{
				writer.WriteAttributeString("type", $"tns:{typeName}");
			}
		}

		private string GetTypeName(Type type)
		{
			if (type.IsGenericType && !type.IsArray && !typeof(IEnumerable).IsAssignableFrom(type))
			{
				var genericType = GetGenericType(type);
				var genericTypeName = GetTypeName(genericType);

				var typeName = type.Name.Replace("`1", string.Empty);
				typeName = typeName + "Of" + genericTypeName;
				return typeName;
			}

			if (type.IsArray)
			{
				return "ArrayOf" + GetTypeName(type.GetElementType());
			}

			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				return "ArrayOf" + GetTypeName(GetGenericType(type));
			}

			// Make use of DataContract attribute, if set, as it may contain a Name override
			var dataContractAttribute = type.GetCustomAttribute<DataContractAttribute>();
			if (dataContractAttribute != null && !string.IsNullOrEmpty(dataContractAttribute.Name))
			{
				return dataContractAttribute.Name;
			}

			return type.Name;
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

		private bool HasBaseType(Type type)
		{
			var isArrayType = type.IsArray || typeof(IEnumerable).IsAssignableFrom(type);

			var baseType = type.GetTypeInfo().BaseType;

			return !isArrayType && !type.IsEnum && !type.IsPrimitive && !type.IsValueType && baseType != null && !baseType.Name.Equals("Object");
		}
	}
}
