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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SoapCore.ServiceModel;

namespace SoapCore.Meta
{
	public class MetaWCFBodyWriter : BodyWriter
	{
#pragma warning disable SA1009 // Closing parenthesis must be spaced correctly
#pragma warning disable SA1008 // Opening parenthesis must be spaced correctly
		private static readonly Dictionary<string, (string, string)> SysTypeDic = new Dictionary<string, (string, string)>()
		{
			["System.String"] = ("string", Namespaces.SYSTEM_NS),
			["System.Boolean"] = ("boolean", Namespaces.SYSTEM_NS),
			["System.Int16"] = ("short", Namespaces.SYSTEM_NS),
			["System.Int32"] = ("int", Namespaces.SYSTEM_NS),
			["System.Int64"] = ("long", Namespaces.SYSTEM_NS),
			["System.Byte"] = ("byte", Namespaces.SYSTEM_NS),
			["System.SByte"] = ("byte", Namespaces.SYSTEM_NS),
			["System.UInt16"] = ("unsignedShort", Namespaces.SYSTEM_NS),
			["System.UInt32"] = ("unsignedInt", Namespaces.SYSTEM_NS),
			["System.UInt64"] = ("unsignedLong", Namespaces.SYSTEM_NS),
			["System.Decimal"] = ("decimal", Namespaces.SYSTEM_NS),
			["System.Double"] = ("double", Namespaces.SYSTEM_NS),
			["System.Single"] = ("float", Namespaces.SYSTEM_NS),
			["System.DateTime"] = ("dateTime", Namespaces.SYSTEM_NS),
			["System.Guid"] = ("guid", Namespaces.SERIALIZATION_NS),
			["System.Char"] = ("char", Namespaces.SERIALIZATION_NS),
			["System.TimeSpan"] = ("duration", Namespaces.SERIALIZATION_NS),
			["System.Object"] = ("anyType", Namespaces.SERIALIZATION_NS),
			["System.Uri"] = ("anyUri", Namespaces.SERIALIZATION_NS),
		};
#pragma warning restore SA1008 // Opening parenthesis must be spaced correctly
#pragma warning restore SA1009 // Closing parenthesis must be spaced correctly

		private static int _namespaceCounter = 1;

		private readonly ServiceDescription _service;
		private readonly string _baseUrl;

		private readonly string[] _numbers = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

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

		[Obsolete]
		public MetaWCFBodyWriter(ServiceDescription service, string baseUrl, Binding binding)
			: this(
				  service,
				  baseUrl,
				  binding?.Name ?? "BasicHttpBinding_" + service.GeneralContract.Name,
				  binding.HasBasicAuth(),
				  new[] { new SoapBindingInfo(binding.MessageVersion ?? MessageVersion.None, null, null) })
		{
		}

		public MetaWCFBodyWriter(ServiceDescription service, string baseUrl, string bindingName, bool hasBasicAuthentication, SoapBindingInfo[] soapBindings) : base(isBuffered: true)
		{
			_service = service;
			_baseUrl = baseUrl;

			_arrayToBuild = new Queue<Type>();
			_builtEnumTypes = new HashSet<string>();
			_builtComplexTypes = new HashSet<string>();
			_buildArrayTypes = new HashSet<string>();
			_builtSerializationElements = new HashSet<string>();

			BindingType = service.GeneralContract.Name;
			TargetNameSpace = service.GeneralContract.Namespace;

			BindingName = bindingName;
			PortName = bindingName;
			HasBasicAuthentication = hasBasicAuthentication;
			SoapBindings = soapBindings;
		}

		private SoapBindingInfo[] SoapBindings { get; }
		private bool HasBasicAuthentication { get; }
		private string BindingName { get; }
		private string BindingType { get; }
		private string PortName { get; }
		private string TargetNameSpace { get; }

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			AddTypes(writer);

			AddMessages(writer);

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

			return $"{Namespaces.DataContractNamespace}{@namespace}";
		}

		private static string GetDataContractNamespace(Type type)
		{
			if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
			{
				var collectionDataContractAttribute = type.GetCustomAttribute<CollectionDataContractAttribute>();
				if (collectionDataContractAttribute != null && collectionDataContractAttribute.IsNamespaceSetExplicitly)
				{
					return collectionDataContractAttribute.Namespace;
				}
				else
				{
					type = type.IsArray ? type.GetElementType() : GetGenericType(type);
				}
			}

			var dataContractAttribute = type.GetCustomAttribute<DataContractAttribute>();
			if (dataContractAttribute != null && dataContractAttribute.IsNamespaceSetExplicitly)
			{
				return dataContractAttribute.Namespace;
			}

			return GetModelNamespace(type.Namespace);
		}

		private static Type GetGenericType(Type collectionType)
		{
			return GetGenericTypes(collectionType).DefaultIfEmpty(typeof(object)).FirstOrDefault();
		}

		private static Type[] GetGenericTypes(Type collectionType)
		{
			// Recursively look through the base class to find the Generic Type of the Enumerable
			var baseType = collectionType;

			var collectionInterfaceTypeInfo = baseType.GetInterfaces().Where(a => a.Name == "ICollection`1").FirstOrDefault();
			if (collectionInterfaceTypeInfo != null)
			{
				//handle Dictionary KeyValuePair's and other collections with more than one generic parameter as a simple return type
				return collectionInterfaceTypeInfo.GetGenericArguments();
			}

			var baseTypeInfo = collectionType.GetTypeInfo();
			while (!baseTypeInfo.IsGenericType && baseTypeInfo.BaseType != null)
			{
				baseType = baseTypeInfo.BaseType;
				baseTypeInfo = baseType.GetTypeInfo();
			}

			return baseType.GetTypeInfo().GetGenericArguments();
		}

		private string GetModelNamespace(Type type)
		{
			if (type != null && type.Namespace != _service.ServiceType.Namespace)
			{
				return $"{Namespaces.DataContractNamespace}{type.Namespace}";
			}

			return $"{Namespaces.DataContractNamespace}{_service.ServiceType.Namespace}";
		}

		private void WriteParameters(XmlDictionaryWriter writer, SoapMethodParameterInfo[] parameterInfos)
		{
			foreach (var parameterInfo in parameterInfos)
			{
				if (parameterInfo.Parameter.ParameterType.FullName == "System.Threading.CancellationToken")
					continue;
				var elementAttribute = parameterInfo.Parameter.GetCustomAttribute<XmlElementAttribute>();
				var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
										? elementAttribute.ElementName
										: parameterInfo.Parameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameterInfo.Parameter.Name;
				var isRequired = !parameterInfo.Parameter.IsOptional;
				AddSchemaType(writer, parameterInfo.Parameter.ParameterType, parameterName, objectNamespace: GetDataContractNamespace(parameterInfo.Parameter.ParameterType), isRequired: isRequired);
			}
		}

		private void EnsureServiceKnownTypes(IEnumerable<ServiceKnownTypeAttribute> serviceKnownTypes)
		{
			foreach (ServiceKnownTypeAttribute knownType in serviceKnownTypes)
			{
				if (knownType.Type is not null)
				{
					AddKnownType(knownType.Type);
				}
				else if (knownType.DeclaringType is not null && !string.IsNullOrWhiteSpace(knownType.MethodName))
				{
					var method = knownType.DeclaringType.GetMethod(knownType.MethodName, BindingFlags.Static | BindingFlags.Public);
					if (method is null)
					{
						throw new NotSupportedException($"Method `{knownType.MethodName}` doesn't exist on Type `{knownType.DeclaringType.FullName}`.");
					}

					var knownTypeList = method.Invoke(null, new object[] { (ICustomAttributeProvider)null }) as IEnumerable;
					if (knownTypeList is null)
					{
						throw new NotSupportedException($"Method `{knownType.MethodName}` on Type `{knownType.DeclaringType.FullName}` returned null or not an IEnumerable.");
					}

					foreach (var type in knownTypeList.OfType<Type>())
					{
						if (type != null)
						{
							AddKnownType(type);
						}
					}
				}
				else
				{
					throw new NotSupportedException($"You must specify Type property or DeclaringType and MethodName properties of `{nameof(ServiceKnownTypeAttribute)}`.");
				}
			}
		}

		private void AddKnownType(Type type)
		{
			// Add service known type
			_complexTypeToBuild[type] = GetDataContractNamespace(type);

			// Discover type known types
			DiscoverTypes(type, false);
		}

		private void AddContractOperations(XmlDictionaryWriter writer, ContractDescription contract)
		{
			IEnumerable<OperationDescription> operations = contract.Operations;

			writer.WriteStartElement("xs", "schema", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", contract.Namespace);
			writer.WriteXmlnsAttribute("xs", Namespaces.XMLNS_XSD);
			writer.WriteXmlnsAttribute("ser", Namespaces.SERIALIZATION_NS);

			_schemaNamespace = TargetNameSpace;
			_namespaceCounter = 1;

			//discovery all parameters types which namespaceses diff with service namespace
			foreach (OperationDescription operation in operations)
			{
				// Ensure operation service known type attributes
				EnsureServiceKnownTypes(operation.ServiceKnownTypes);

				foreach (var parameter in operation.AllParameters)
				{
					var type = parameter.Parameter.ParameterType;
					var typeInfo = type.GetTypeInfo();
					if (typeInfo.FullName == "System.Threading.CancellationToken")
						continue;
					if (typeInfo.IsByRef)
					{
						type = typeInfo.GetElementType();
					}

					if (TypeIsComplexForWsdl(type, out type))
					{
						_complexTypeToBuild[type] = GetDataContractNamespace(type);
						DiscoverTypes(type, true);
					}
					else if (type.IsEnum || Nullable.GetUnderlyingType(type)?.IsEnum == true)
					{
						_complexTypeToBuild[type] = GetDataContractNamespace(type);
						DiscoverTypes(type, true);
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
						DiscoverTypes(returnType, true);
					}
					else if (returnType.IsEnum || Nullable.GetUnderlyingType(returnType)?.IsEnum == true)
					{
						_complexTypeToBuild[returnType] = GetDataContractNamespace(returnType);
						DiscoverTypes(returnType, true);
					}
				}
			}

			var groupedByNamespace = _complexTypeToBuild.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.Select(k => k.Key));

			foreach (var @namespace in groupedByNamespace.Keys.Where(x => x != null && x != _service.ServiceType.Namespace && x != contract.Namespace).Distinct())
			{
				writer.WriteStartElement("xs", "import", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("namespace", @namespace);
				writer.WriteEndElement();
			}

			foreach (OperationDescription operation in operations)
			{
				// input parameters of operation
				writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", operation.Name);
				writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
				writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);

				WriteParameters(writer, operation.InParameters);

				writer.WriteEndElement(); // xs:sequence
				writer.WriteEndElement(); // xs:complexType
				writer.WriteEndElement(); // xs:element

				// output parameter / return of operation
				writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", operation.Name + "Response");
				writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
				writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);

				if (operation.DispatchMethod.ReturnType != typeof(void) && operation.DispatchMethod.ReturnType != typeof(Task))
				{
					var returnType = operation.DispatchMethod.ReturnType;
					if (returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					{
						returnType = returnType.GetGenericArguments().First();
					}

					var returnName = operation.DispatchMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? operation.Name + "Result";
					var isRequired = !operation.DispatchMethod.ReturnParameter.IsOptional;
					AddSchemaType(writer, returnType, returnName, false, GetDataContractNamespace(returnType), isRequired: isRequired);
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
				DiscoverTypes(faultType, true);
			}
		}

		private void AddTypes(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl", "types", Namespaces.WSDL_NS);

			// Ensure service known type attributes
			// TODO: should we parse only contact attributes ??
			EnsureServiceKnownTypes(_service.ServiceKnownTypes);

			// Ensure service contract service known type attributes
			EnsureServiceKnownTypes(_service.Contracts.SelectMany(x => x.ServiceKnownTypes));

			foreach (ContractDescription contract in _service.Contracts)
			{
				AddContractOperations(writer, contract);
			}

			AddMSSerialization(writer);
			AddComplexTypes(writer);
			AddArrayTypes(writer);
			AddSystemTypes(writer);

			writer.WriteEndElement();
		}

		private void AddSystemTypes(XmlDictionaryWriter writer)
		{
			if (_buildDateTimeOffset)
			{
				writer.WriteStartElement("xs", "schema", Namespaces.XMLNS_XSD);
				writer.WriteXmlnsAttribute("xs", Namespaces.XMLNS_XSD);
				writer.WriteXmlnsAttribute("tns", Namespaces.SYSTEM_NS);
				writer.WriteAttributeString("elementFormDefault", "qualified");
				writer.WriteAttributeString("targetNamespace", Namespaces.SYSTEM_NS);

				writer.WriteStartElement("xs", "import", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("namespace", Namespaces.SERIALIZATION_NS);
				writer.WriteEndElement();

				writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", "DateTimeOffset");
				writer.WriteStartElement("xs", "annotation", Namespaces.XMLNS_XSD);
				writer.WriteStartElement("xs", "appinfo", Namespaces.XMLNS_XSD);

				writer.WriteElementString("IsValueType", Namespaces.SERIALIZATION_NS, "true");
				writer.WriteEndElement(); // xs:appinfo
				writer.WriteEndElement(); // xs:annotation

				writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);

				writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", "DateTime");
				writer.WriteAttributeString("type", "xs:dateTime");
				writer.WriteEndElement();

				writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", "OffsetMinutes");
				writer.WriteAttributeString("type", "xs:short");
				writer.WriteEndElement();

				writer.WriteEndElement(); // xs:sequence

				writer.WriteEndElement(); // xs:complexType

				writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", "DateTimeOffset");
				writer.WriteAttributeString("nillable", "true");
				writer.WriteAttributeString("type", "tns:DateTimeOffset");
				writer.WriteEndElement();

				writer.WriteEndElement(); // xs:schema
			}

			if (_buildDataTable)
			{
				writer.WriteStartElement("xs", "schema", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("elementFormDefault", "qualified");
				writer.WriteAttributeString("targetNamespace", Namespaces.SystemData_NS);
				writer.WriteXmlnsAttribute("xs", Namespaces.XMLNS_XSD);
				writer.WriteXmlnsAttribute("tns", Namespaces.SystemData_NS);

				writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", "DataTable");
				writer.WriteAttributeString("nillable", "true");

				writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
				writer.WriteStartElement("xs", "annotation", Namespaces.XMLNS_XSD);

				writer.WriteStartElement("xs", "appinfo", Namespaces.XMLNS_XSD);
				writer.WriteStartElement("ActualType");
				writer.WriteAttributeString("xmlns", Namespaces.SERIALIZATION_NS);
				writer.WriteAttributeString("Name", "DataTable");
				writer.WriteAttributeString("Namespace", Namespaces.SystemData_NS);
				writer.WriteEndElement(); //actual type
				writer.WriteEndElement(); //appinfo
				writer.WriteEndElement(); //annotation

				writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);

				writer.WriteStartElement("xs", "any", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("minOccurs", "0");
				writer.WriteAttributeString("maxOccurs", "unbounded");
				writer.WriteAttributeString("namespace", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("processContents", "lax");
				writer.WriteEndElement(); //any

				writer.WriteStartElement("xs", "any", Namespaces.XMLNS_XSD);
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
			writer.WriteStartElement("xs", "schema", Namespaces.XMLNS_XSD);
			writer.WriteXmlnsAttribute("xs", Namespaces.XMLNS_XSD);
			writer.WriteXmlnsAttribute("tns", Namespaces.ARRAYS_NS);
			writer.WriteXmlnsAttribute("ser", Namespaces.SERIALIZATION_NS);
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", Namespaces.ARRAYS_NS);
			_namespaceCounter = 1;
			_schemaNamespace = Namespaces.ARRAYS_NS;

			writer.WriteStartElement("xs", "import", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("namespace", Namespaces.SERIALIZATION_NS);
			writer.WriteEndElement();

			while (_arrayToBuild.Count > 0)
			{
				var toBuild = _arrayToBuild.Dequeue();
				var elType = toBuild.IsArray ? toBuild.GetElementType() : GetGenericType(toBuild);
				var sysType = ResolveSystemType(elType);
				var toBuildName = "ArrayOf" + sysType.name;

				if (!_buildArrayTypes.Contains(toBuildName))
				{
					writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
					writer.WriteAttributeString("name", toBuildName);

					writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);
					AddSchemaType(writer, elType, null, true);
					writer.WriteEndElement(); // :sequence

					writer.WriteEndElement(); // xs:complexType

					writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
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
			writer.WriteStartElement("xs", "schema", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("attributeFormDefault", "qualified");
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", Namespaces.SERIALIZATION_NS);
			writer.WriteXmlnsAttribute("xs", Namespaces.XMLNS_XSD);
			writer.WriteXmlnsAttribute("tns", Namespaces.SERIALIZATION_NS);
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
			writer.WriteStartElement("xs", "simpleType", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("name", "char");
			writer.WriteStartElement("xs", "restriction", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("base", "xs:int");
			writer.WriteEndElement();
			writer.WriteEndElement();

			WriteSerializationElement(writer, "duration", "tns:duration", true);
			writer.WriteStartElement("xs", "simpleType", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("name", "duration");
			writer.WriteStartElement("xs", "restriction", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("base", "xs:duration");
			writer.WriteStartElement("xs", "pattern", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("value", @"\-?P(\d*D)?(T(\d*H)?(\d*M)?(\d*(\.\d*)?S)?)?");
			writer.WriteEndElement();
			writer.WriteStartElement("xs", "minInclusive", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("value", @"-P10675199DT2H48M5.4775808S");
			writer.WriteEndElement();
			writer.WriteStartElement("xs", "maxInclusive", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("value", @"P10675199DT2H48M5.4775807S");
			writer.WriteEndElement();
			writer.WriteEndElement();
			writer.WriteEndElement();

			WriteSerializationElement(writer, "guid", "tns:guid", true);
			writer.WriteStartElement("xs", "simpleType", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("name", "guid");
			writer.WriteStartElement("xs", "restriction", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("base", "xs:string");
			writer.WriteStartElement("xs", "pattern", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("value", @"[\da-fA-F]{8}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{12}");
			writer.WriteEndElement();
			writer.WriteEndElement();
			writer.WriteEndElement();

			writer.WriteStartElement("xs", "attribute", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("name", "FactoryType");
			writer.WriteAttributeString("type", "xs:QName");
			writer.WriteEndElement();

			writer.WriteStartElement("xs", "attribute", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("name", "Id");
			writer.WriteAttributeString("type", "xs:ID");
			writer.WriteEndElement();

			writer.WriteStartElement("xs", "attribute", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("name", "Ref");
			writer.WriteAttributeString("type", "xs:IDREF");
			writer.WriteEndElement();

			writer.WriteEndElement(); //schema
		}

		private void WriteSerializationElement(XmlDictionaryWriter writer, string name, string type, bool nillable)
		{
			if (!_builtSerializationElements.Contains(name))
			{
				writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
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
				DiscoverTypes(type.Key, true);
			}

			var groupedByNamespace = _complexTypeToBuild.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.Select(k => k.Key));

			foreach (var types in groupedByNamespace.Distinct())
			{
				writer.WriteStartElement("xs", "schema", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("elementFormDefault", "qualified");
				writer.WriteAttributeString("targetNamespace", GetModelNamespace(types.Key));
				writer.WriteXmlnsAttribute("xs", Namespaces.XMLNS_XSD);
				writer.WriteXmlnsAttribute("tns", GetModelNamespace(types.Key));
				writer.WriteXmlnsAttribute("ser", Namespaces.SERIALIZATION_NS);

				_namespaceCounter = 1;
				_schemaNamespace = GetModelNamespace(types.Key);

				writer.WriteStartElement("xs", "import", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("namespace", Namespaces.SYSTEM_NS);
				writer.WriteEndElement();

				writer.WriteStartElement("xs", "import", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("namespace", Namespaces.ARRAYS_NS);
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

					writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);
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

		private void DiscoverTypes(Type type, bool isRootType)
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

			// discover known types
			IEnumerable<KnownTypeAttribute> knownTypes = type.GetCustomAttributes<KnownTypeAttribute>(inherit: false);
			foreach (KnownTypeAttribute knownType in knownTypes)
			{
				if (knownType.Type is not null)
				{
					// add known type
					_complexTypeToBuild[knownType.Type] = GetDataContractNamespace(knownType.Type);

					// discover recursive
					DiscoverTypes(knownType.Type, false);
				}
				else if (!string.IsNullOrWhiteSpace(knownType.MethodName))
				{
					var method = type.GetMethod(knownType.MethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					if (method is null)
					{
						throw new NotSupportedException($"Method `{knownType.MethodName}` doesn't exist on Type `{type.FullName}`.");
					}

					var knownTypeList = method.Invoke(null, new object[0]) as IEnumerable;
					if (knownTypeList is null)
					{
						throw new NotSupportedException($"Method `{knownType.MethodName}` on Type `{type.FullName}` returned null or not an IEnumerable.");
					}

					foreach (var item in knownTypeList.OfType<Type>())
					{
						if (item != null)
						{
							// add known type
							_complexTypeToBuild[item] = GetDataContractNamespace(item);

							// discover recursive
							DiscoverTypes(item, false);
						}
					}
				}
				else
				{
					throw new NotSupportedException($"You must specify Type property or MethodName properties of `{nameof(KnownTypeAttribute)}`.");
				}
			}

			if (HasBaseType(type) && type.BaseType != null)
			{
				_complexTypeToBuild[type.BaseType] = GetDataContractNamespace(type.BaseType);
				DiscoverTypes(type.BaseType, false);
			}

			if ((type.IsArray || typeof(IEnumerable).IsAssignableFrom(type)) && type.IsGenericType)
			{
				var genericType = GetGenericType(type);
				var (name, _) = ResolveSystemType(genericType);
				if (string.IsNullOrEmpty(name))
				{
					_complexTypeToBuild[genericType] = GetDataContractNamespace(genericType);
					DiscoverTypes(genericType, true);
				}
			}

			foreach (var member in type.GetPropertyOrFieldMembers().Where(mi =>
						mi.DeclaringType == type
						&& mi.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute")
						&& (!mi.DeclaringType.CustomAttributes.Any(x => x.AttributeType == typeof(DataContractAttribute))
							|| mi.CustomAttributes.Any(x => x.AttributeType == typeof(DataMemberAttribute)))
						&& !mi.GetPropertyOrFieldType().IsPrimitive
						&& !SysTypeDic.ContainsKey(mi.GetPropertyOrFieldType().FullName)
						&& mi.GetPropertyOrFieldType() != typeof(ValueType)
						&& mi.GetPropertyOrFieldType() != typeof(DateTimeOffset)))
			{
				Type memberType;
				var underlyingType = Nullable.GetUnderlyingType(member.GetPropertyOrFieldType());

				if (Nullable.GetUnderlyingType(member.GetPropertyOrFieldType()) != null)
				{
					memberType = underlyingType;
				}
				else if (member.GetPropertyOrFieldType().IsArray || typeof(IEnumerable).IsAssignableFrom(member.GetPropertyOrFieldType()))
				{
					memberType = member.GetPropertyOrFieldType().IsArray
						? member.GetPropertyOrFieldType().GetElementType()
						: GetGenericType(member.GetPropertyOrFieldType());
					_complexTypeToBuild[member.GetPropertyOrFieldType()] = GetDataContractNamespace(member.GetPropertyOrFieldType());
				}
				else
				{
					memberType = member.GetPropertyOrFieldType();
				}

				if (memberType != null && !memberType.IsPrimitive && !SysTypeDic.ContainsKey(memberType.FullName))
				{
					if (memberType == type)
					{
						continue;
					}

					_complexTypeToBuild[memberType] = GetDataContractNamespace(memberType);
					DiscoverTypes(memberType, false);
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
				writer.WriteStartElement("xs", "simpleType", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("name", typeName);
				writer.WriteStartElement("xs", "restriction", Namespaces.XMLNS_XSD);
				writer.WriteAttributeString("base", "xs:string");

				foreach (var name in Enum.GetNames(type))
				{
					writer.WriteStartElement("xs", "enumeration", Namespaces.XMLNS_XSD);

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

			if (_builtComplexTypes.Contains(type.FullName))
			{
				return;
			}

			writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
			writer.WriteAttributeString("name", toBuildName);
			writer.WriteAttributeString("xmlns", "ser", null, Namespaces.SERIALIZATION_NS);

			if (type.IsValueType && ResolveSystemType(type).name == null)
			{
				writer.WriteStartElement("xs", "annotation", Namespaces.XMLNS_XSD);
				writer.WriteStartElement("xs", "appinfo", Namespaces.XMLNS_XSD);
				writer.WriteStartElement("IsValueType", Namespaces.SERIALIZATION_NS);
				writer.WriteValue(true);
				writer.WriteEndElement();
				writer.WriteEndElement();
				writer.WriteEndElement();
			}

			var hasBaseType = HasBaseType(type);

			if (hasBaseType)
			{
				writer.WriteStartElement("xs", "complexContent", Namespaces.XMLNS_XSD);

				writer.WriteAttributeString("mixed", "false");

				writer.WriteStartElement("xs", "extension", Namespaces.XMLNS_XSD);

				var modelNamespace = GetDataContractNamespace(type.BaseType);

				var typeName = GetTypeName(type.BaseType);

				if (_schemaNamespace != modelNamespace)
				{
					var ns = $"q{_namespaceCounter++}";
					writer.WriteAttributeString("base", $"{ns}:{typeName}");
					writer.WriteAttributeString("xmlns", ns, null, modelNamespace);
				}
				else
				{
					writer.WriteAttributeString("base", $"tns:{typeName}");
				}
			}

			writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);

			if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type))
			{
				var elementType = type.IsArray ? type.GetElementType() : GetGenericType(type);

				string elementName = null;
				var collectionDataContractAttribute = type.GetCustomAttribute<CollectionDataContractAttribute>();
				if (collectionDataContractAttribute != null && collectionDataContractAttribute.IsItemNameSetExplicitly)
				{
					elementName = collectionDataContractAttribute.ItemName;
				}

				AddSchemaType(writer, elementType, elementName, true, GetDataContractNamespace(type));
			}
			else
			{
				var propertyOrFieldMembers = type.GetPropertyOrFieldMembers().Where(mi =>
					mi.DeclaringType == type
					&& mi.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute")
					&& (!mi.DeclaringType.CustomAttributes.Any(x => x.AttributeType == typeof(DataContractAttribute))
						|| mi.CustomAttributes.Any(x => x.AttributeType == typeof(DataMemberAttribute))));

				var dataMembersToWrite = new List<DataMemberDescription>();

				//TODO: base type properties
				//TODO: enforce order attribute parameters
				foreach (var member in propertyOrFieldMembers)
				{
					var memberName = member.Name;

					var attributes = member.GetCustomAttributes(true);
					int order = 0;
					bool isRequired = false;
					foreach (var attr in attributes)
					{
						if (attr is DataMemberAttribute dataContractAttribute)
						{
							if (dataContractAttribute.IsNameSetExplicitly)
							{
								memberName = dataContractAttribute.Name;
							}

							if (dataContractAttribute.Order > 0)
							{
								order = dataContractAttribute.Order;
							}

							isRequired = dataContractAttribute.IsRequired;

							break;
						}
					}

					dataMembersToWrite.Add(new DataMemberDescription
					{
						Name = memberName,
						Type = member.GetPropertyOrFieldType(),
						Order = order,
						IsRequired = isRequired
					});
				}

				foreach (var p in dataMembersToWrite.OrderBy(x => x.Order).ThenBy(p => p.Name, StringComparer.Ordinal))
				{
					AddSchemaType(writer, p.Type, p.Name, false, GetDataContractNamespace(p.Type), p.IsRequired);
				}
			}

			writer.WriteEndElement(); // xs:sequence

			if (hasBaseType)
			{
				writer.WriteEndElement(); // xs:extension
				writer.WriteEndElement(); // xs:complexContent
			}

			writer.WriteEndElement(); // xs:complexType

			_builtComplexTypes.Add(type.FullName);
		}

		private void AddMessages(XmlDictionaryWriter writer)
		{
			foreach (var operation in _service.Operations)
			{
				// input
				writer.WriteStartElement("wsdl", "message", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_InputMessage");
				writer.WriteStartElement("wsdl", "part", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", "parameters");

				string inputElement = "tns:" + operation.Name;
				if (operation.Contract.Name != BindingType)
				{
					var ns = $"q{_namespaceCounter++}";
					writer.WriteXmlnsAttribute($"{ns}", operation.Contract.Namespace);

					inputElement = $"{ns}:{operation.Name}";
				}

				writer.WriteAttributeString("element", inputElement);
				writer.WriteEndElement(); // wsdl:part
				writer.WriteEndElement(); // wsdl:message

				// output
				if (!operation.IsOneWay)
				{
					writer.WriteStartElement("wsdl", "message", Namespaces.WSDL_NS);
					writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_OutputMessage");
					writer.WriteStartElement("wsdl", "part", Namespaces.WSDL_NS);
					writer.WriteAttributeString("name", "parameters");

					string outputElement = "tns:" + operation.Name + "Response";
					if (operation.Contract.Name != BindingType)
					{
						var ns = $"q{_namespaceCounter++}";
						writer.WriteXmlnsAttribute($"{ns}", operation.Contract.Namespace);

						outputElement = $"{ns}:{operation.Name}Response";
					}

					writer.WriteAttributeString("element", outputElement);
					writer.WriteEndElement(); // wsdl:part
					writer.WriteEndElement(); // wsdl:message
				}

				AddMessageFaults(writer, operation);
			}
		}

		private void AddMessageFaults(XmlDictionaryWriter writer, OperationDescription operation)
		{
			foreach (Type fault in operation.Faults)
			{
				writer.WriteStartElement("wsdl", "message", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", $"{BindingType}_{operation.Name}_{fault.Name}Fault_FaultMessage");
				writer.WriteStartElement("wsdl", "part", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", "detail");
				var ns = $"q{_namespaceCounter++}";
				writer.WriteAttributeString("element", $"{ns}:{fault.Name}");
				writer.WriteAttributeString("xmlns", ns, null, GetDataContractNamespace(fault));
				writer.WriteEndElement(); // wsdl:part
				writer.WriteEndElement(); // wsdl:message
			}
		}

		private void AddPortType(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl", "portType", Namespaces.WSDL_NS);
			writer.WriteAttributeString("name", BindingType);
			foreach (var operation in _service.Operations)
			{
				writer.WriteStartElement("wsdl", "operation", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", operation.Name);
				writer.WriteStartElement("wsdl", "input", Namespaces.WSDL_NS);
				writer.WriteAttributeString("wsam", "Action", Namespaces.WSAM_NS, operation.SoapAction);
				writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_InputMessage");
				writer.WriteEndElement(); // wsdl:input

				if (!operation.IsOneWay)
				{
					writer.WriteStartElement("wsdl", "output", Namespaces.WSDL_NS);
					writer.WriteAttributeString("wsam", "Action", Namespaces.WSAM_NS, operation.SoapAction + "Response");
					writer.WriteAttributeString("message", $"tns:{BindingType}_{operation.Name}_OutputMessage");
					writer.WriteEndElement(); // wsdl:output
				}

				AddPortTypeFaults(writer, operation);

				writer.WriteEndElement(); // wsdl:operation
			}

			writer.WriteEndElement(); // wsdl:portType
		}

		private void AddPortTypeFaults(XmlDictionaryWriter writer, OperationDescription operation)
		{
			foreach (Type fault in operation.Faults)
			{
				writer.WriteStartElement("wsdl", "fault", Namespaces.WSDL_NS);
				writer.WriteAttributeString("wsam", "Action", Namespaces.WSAM_NS, $"{operation.SoapAction}{fault.Name}Fault");
				writer.WriteAttributeString("name", $"{fault.Name}Fault");
				writer.WriteAttributeString("message", $"tns:{_service.GeneralContract.Name}_{operation.Name}_{fault.Name}Fault_FaultMessage");
				writer.WriteEndElement(); // wsdl:fault
			}
		}

		private void AddBinding(XmlDictionaryWriter writer)
		{
			foreach (var bindingInfo in SoapBindings)
			{
				(var soap, var soapNamespace, var qualifiedBindingName, _) = GetSoapMetaParameters(bindingInfo);

				writer.WriteStartElement("wsdl", "binding", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", BindingName);
				writer.WriteAttributeString("type", "tns:" + BindingType);

				writer.WriteStartElement(soap, "binding", soapNamespace);
				writer.WriteAttributeString("transport", Namespaces.TRANSPORT_SCHEMA);
				writer.WriteEndElement(); // soap:binding

				foreach (var operation in _service.Operations)
				{
					writer.WriteStartElement("wsdl", "operation", Namespaces.WSDL_NS);
					writer.WriteAttributeString("name", operation.Name);

					writer.WriteStartElement(soap, "operation", soapNamespace);
					writer.WriteAttributeString("soapAction", operation.SoapAction);
					writer.WriteAttributeString("style", "document");
					writer.WriteEndElement(); // soap:operation

					writer.WriteStartElement("wsdl", "input", Namespaces.WSDL_NS);
					writer.WriteStartElement(soap, "body", soapNamespace);
					writer.WriteAttributeString("use", "literal");
					writer.WriteEndElement(); // soap:body
					writer.WriteEndElement(); // wsdl:input

					if (!operation.IsOneWay)
					{
						writer.WriteStartElement("wsdl", "output", Namespaces.WSDL_NS);
						writer.WriteStartElement(soap, "body", soapNamespace);
						writer.WriteAttributeString("use", "literal");
						writer.WriteEndElement(); // soap:body
						writer.WriteEndElement(); // wsdl:output
					}

					AddBindingFaults(writer, operation, soap, soapNamespace);

					writer.WriteEndElement(); // wsdl:operation
				}

				writer.WriteEndElement(); // wsdl:binding
			}
		}

		private void AddBindingFaults(XmlDictionaryWriter writer, OperationDescription operation, string soap, string soapNamespace)
		{
			foreach (Type fault in operation.Faults)
			{
				writer.WriteStartElement("wsdl", "fault", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", $"{fault.Name}Fault");

				writer.WriteStartElement(soap, "fault", soapNamespace);
				writer.WriteAttributeString("use", "literal");
				writer.WriteAttributeString("name", $"{fault.Name}Fault");
				writer.WriteEndElement(); // soap:fault

				writer.WriteEndElement(); // wsdl:fault
			}
		}

		private void AddService(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("wsdl", "service", Namespaces.WSDL_NS);
			writer.WriteAttributeString("name", _service.ServiceName);

			foreach (var bindingInfo in SoapBindings)
			{
				(var soap, var soapNamespace, var qualifiedBindingName, var qualifiedPortName) = GetSoapMetaParameters(bindingInfo);

				writer.WriteStartElement("wsdl", "port", Namespaces.WSDL_NS);
				writer.WriteAttributeString("name", qualifiedPortName);
				writer.WriteAttributeString("binding", "tns:" + qualifiedBindingName);

				writer.WriteStartElement(soap, "address", soapNamespace);

				writer.WriteAttributeString("location", _baseUrl);
				writer.WriteEndElement(); // soap:address

				writer.WriteEndElement(); // wsdl:port
			}
		}

		private void AddSchemaType(XmlDictionaryWriter writer, Type type, string name, bool isArray = false, string objectNamespace = null, bool isRequired = false)
		{
			if (typeof(IActionResult).IsAssignableFrom(type) || typeof(IConvertToActionResult).IsAssignableFrom(type))
			{
				var genericArgs = type.GetGenericArguments();

				if (genericArgs.Length > 0)
				{
					type = genericArgs[0];
				}
				else
				{
					type = typeof(object);
				}
			}

			var typeInfo = type.GetTypeInfo();
			var typeName = GetTypeName(type);

			if (typeInfo.IsByRef)
			{
				type = typeInfo.GetElementType();
			}

			if (writer.TryAddSchemaTypeFromXmlSchemaProviderAttribute(type, name, SoapSerializer.DataContractSerializer))
			{
				return;
			}

			writer.WriteStartElement("xs", "element", Namespaces.XMLNS_XSD);

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

				if (isArray)
				{
					writer.WriteAttributeString("minOccurs", isRequired ? "1" : "0");
					writer.WriteAttributeString("maxOccurs", "unbounded");
				}
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
					writer.WriteXmlnsAttribute($"{ns}", Namespaces.SYSTEM_NS);

					_buildDateTimeOffset = true;
				}
				else
				{
					Type underlyingType = Nullable.GetUnderlyingType(type);
					if (underlyingType != null)
					{
						objectNamespace = GetDataContractNamespace(underlyingType);
						typeName = GetTypeName(underlyingType);

						if (ResolveSystemType(underlyingType).name != null)
						{
							var sysType = ResolveSystemType(underlyingType);
							xsTypename = $"{(sysType.ns == Namespaces.SERIALIZATION_NS ? "ser" : "xs")}:{sysType.name}";
							writer.WriteAttributeString("nillable", "true");
						}
						else if (_schemaNamespace != objectNamespace)
						{
							var ns = $"q{_namespaceCounter++}";
							writer.WriteXmlnsAttribute($"{ns}", GetDataContractNamespace(type));

							xsTypename = $"{ns}:{typeName}";
						}
						else
						{
							xsTypename = $"tns:{typeName}";
						}
					}
					else
					{
						if (ResolveSystemType(type).name != null)
						{
							var sysType = ResolveSystemType(type);
							xsTypename = $"{(sysType.ns == Namespaces.SERIALIZATION_NS ? "ser" : "xs")}:{sysType.name}";
						}
						else if (_schemaNamespace != objectNamespace)
						{
							var ns = $"q{_namespaceCounter++}";
							writer.WriteXmlnsAttribute($"{ns}", GetDataContractNamespace(type));

							xsTypename = $"{ns}:{typeName}";
						}
						else
						{
							xsTypename = $"tns:{typeName}";
						}
					}
				}

				writer.WriteAttributeString("minOccurs", isRequired ? "1" : "0");
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
				writer.WriteAttributeString("minOccurs", isRequired ? "1" : "0");
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
					if (string.IsNullOrEmpty(name))
					{
						name = "anyType";
					}

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "xs:anyType");
				}
				else if (type.Name == "Uri" || type.Name == "Uri&")
				{
					if (string.IsNullOrEmpty(name))
					{
						name = "anyURI";
					}

					// TODO: should we compare namespace to `System.Uri` or ensure type assembly is mscorelib/System
					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("type", "xs:anyURI");
				}
				else if (type == typeof(DataTable))
				{
					_buildDataTable = true;

					writer.WriteAttributeString("name", name);
					writer.WriteAttributeString("nillable", "true");
					writer.WriteStartElement("xs", "complexType", Namespaces.XMLNS_XSD);
					writer.WriteStartElement("xs", "annotation", Namespaces.XMLNS_XSD);
					writer.WriteStartElement("xs", "appinfo", Namespaces.XMLNS_XSD);
					writer.WriteStartElement("ActualType");
					writer.WriteAttributeString("xmlns", Namespaces.SERIALIZATION_NS);
					writer.WriteAttributeString("Name", "DataTable");
					writer.WriteAttributeString("Namespace", Namespaces.SystemData_NS);
					writer.WriteEndElement(); //actual type
					writer.WriteEndElement(); // appinfo
					writer.WriteEndElement(); //annotation
					writer.WriteEndElement(); //complex type

					writer.WriteStartElement("xs", "sequence", Namespaces.XMLNS_XSD);

					writer.WriteStartElement("xs", "any", Namespaces.XMLNS_XSD);
					writer.WriteAttributeString("minOccurs", "0");
					writer.WriteAttributeString("maxOccurs", "unbounded");
					writer.WriteAttributeString("namespace", Namespaces.XMLNS_XSD);
					writer.WriteAttributeString("processContents", "lax");
					writer.WriteEndElement();

					writer.WriteStartElement("xs", "any", Namespaces.XMLNS_XSD);
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
					var elType = type;

					var collectionDataContractAttribute = type.GetCustomAttribute<CollectionDataContractAttribute>();
					if (collectionDataContractAttribute == null)
					{
						elType = elType.IsArray ? type.GetElementType() : GetGenericType(type);
					}

					var sysType = ResolveSystemType(elType);
					if (sysType.name != null)
					{
						if (string.IsNullOrEmpty(name))
						{
							name = typeName;
						}

						var ns = $"q{_namespaceCounter++}";

						writer.WriteXmlnsAttribute($"{ns}", Namespaces.ARRAYS_NS);
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
			resultType = type;

			if (typeInfo.IsByRef)
			{
				type = typeInfo.GetElementType();
			}

			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				var collectionDataContractAttribute = type.GetCustomAttribute<CollectionDataContractAttribute>();
				if (collectionDataContractAttribute != null)
				{
					return true;
				}

				resultType = type.IsArray ? type.GetElementType() : GetGenericType(type);
				type = resultType;
			}

			if (type == typeof(DateTimeOffset))
			{
				return false;
			}

			if (typeInfo.IsEnum)
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
				writer.WriteXmlnsAttribute($"{ns}", GetDataContractNamespace(type));
				writer.WriteAttributeString("type", $"{ns}:{typeName}");
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
				var genericTypes = GetGenericTypes(type);
				var genericTypeNames = genericTypes.Select(a => GetTypeName(a));

				var typeName = ReplaceGenericNames(type.Name);
				typeName = typeName + "Of" + string.Concat(genericTypeNames);
				return typeName;
			}

			if (type.IsArray)
			{
				return "ArrayOf" + GetTypeName(type.GetElementType());
			}

			//Special case as string is IEnumerable and we don't want to turn it into System.Object
			if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
			{
				var collectionDataContractAttribute = type.GetCustomAttribute<CollectionDataContractAttribute>();
				if (collectionDataContractAttribute != null)
				{
					var typeName = collectionDataContractAttribute.IsNameSetExplicitly
						? collectionDataContractAttribute.Name
						: ReplaceGenericNames(type.Name);

					if (type.IsGenericType)
					{
						var genericType = GetGenericType(type);

						var (name, _) = ResolveSystemType(genericType);
						var genericTypeName = string.IsNullOrEmpty(name) ? GetTypeName(genericType) : name;

						typeName = string.Format(typeName, genericTypeName);
					}

					return typeName;
				}
				else
				{
					return "ArrayOf" + GetTypeName(GetGenericType(type));
				}
			}

			// Make use of DataContract attribute, if set, as it may contain a Name override
			var dataContractAttribute = type.GetCustomAttribute<DataContractAttribute>();
			if (dataContractAttribute != null && dataContractAttribute.IsNameSetExplicitly)
			{
				return dataContractAttribute.Name;
			}

			return type.Name;
		}

		private string ReplaceGenericNames(string name)
		{
			if (name.Contains("`"))
			{
				//Regex would be easier
				foreach (var number in _numbers)
				{
					name = name.Replace("`" + number, "`" + string.Empty);
				}

				return name.Replace("`", string.Empty);
			}
			else
			{
				return name;
			}
		}

		private (string name, string ns) ResolveSystemType(Type type)
		{
			if (SysTypeDic.ContainsKey(type.FullName))
			{
				return SysTypeDic[type.FullName];
			}

			return (null, null);
		}

		private bool HasBaseType(Type type)
		{
			var isArrayType = type.IsArray || typeof(IEnumerable).IsAssignableFrom(type);

			var baseType = type.GetTypeInfo().BaseType;

			return !isArrayType && !type.IsEnum && !type.IsPrimitive && !type.IsValueType && baseType != null && !baseType.Name.Equals("Object");
		}

		private (string soapPrefix, string ns, string qualifiedBindingName, string qualifiedPortName) GetSoapMetaParameters(SoapBindingInfo bindingInfo)
		{
			int soapVersion = 11;
			if (bindingInfo.MessageVersion == MessageVersion.Soap12WSAddressingAugust2004 || bindingInfo.MessageVersion == MessageVersion.Soap12WSAddressing10)
			{
				soapVersion = 12;
			}

			(var soapPrefix, var ns) = soapVersion == 12 ? ("soap12", Namespaces.SOAP12_NS) : ("soap", Namespaces.SOAP11_NS);

			var qualifiedBindingName = !string.IsNullOrWhiteSpace(bindingInfo.BindingName) ? bindingInfo.BindingName : BindingName;
			var qualifiedPortName = !string.IsNullOrWhiteSpace(bindingInfo.PortName) ? bindingInfo.PortName : PortName;

			return (soapPrefix, ns, qualifiedBindingName, qualifiedPortName);
		}
	}
}
