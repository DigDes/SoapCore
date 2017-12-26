using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	using System.ServiceModel;

	public class MetaBodyWriter : BodyWriter
	{
		private const string XMLNS_XS = "http://www.w3.org/2001/XMLSchema";
		private const string TRANSPORT_SCHEMA = "http://schemas.xmlsoap.org/soap/http";

		private readonly ServiceDescription _service;
		private readonly string _baseUrl;

		private readonly Queue<Type> _enumToBuild;
		private readonly Queue<Type> _complexTypeToBuild;
		private readonly HashSet<string> _builtEnumTypes;
		private readonly HashSet<string> _builtComplexTypes;

		private string BindingName => "BasicHttpBinding_" + _service.Contracts.First().Name;
		private string BindingType => _service.Contracts.First().Name;
		private string PortName => "BasicHttpBinding_" + _service.Contracts.First().Name;
		private string TargetNameSpace => _service.Contracts.First().Namespace;

		public MetaBodyWriter(ServiceDescription service, string baseUrl) : base(isBuffered: true)
		{
			_service = service;
			_baseUrl = baseUrl;

			_enumToBuild = new Queue<Type>();
			_complexTypeToBuild = new Queue<Type>();
			_builtEnumTypes = new HashSet<string>();
			_builtComplexTypes = new HashSet<string>();
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			AddTypes(writer);

			AddMessage(writer);

			AddPortType(writer);

			AddBinding(writer);

			AddService(writer);
		}

		private void AddTypes(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("xs:schema");
			writer.WriteAttributeString("xmlns:xs", XMLNS_XS);
			writer.WriteAttributeString("elementFormDefault", "qualified");
			writer.WriteAttributeString("targetNamespace", TargetNameSpace);

			foreach (var operation in _service.Operations)
			{
				// input parameters of operation
				writer.WriteStartElement("xs:element");
				writer.WriteAttributeString("name", operation.Name);
				writer.WriteStartElement("xs:complexType");
				writer.WriteStartElement("xs:sequence");

				foreach (var parameter in operation.DispatchMethod.GetParameters().Where(x => !x.IsOut && !x.ParameterType.IsByRef))
				{
					var elementAttribute = parameter.GetCustomAttribute<XmlElementAttribute>();
					var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
						                    ? elementAttribute.ElementName
						                    : parameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameter.Name;
					AddSchemaType(writer, parameter.ParameterType, parameterName, @namespace: elementAttribute?.Namespace);
				}

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
					AddSchemaType(writer, returnType, operation.Name + "Result");
				}

				writer.WriteEndElement(); // xs:sequence
				writer.WriteEndElement(); // xs:complexType
				writer.WriteEndElement(); // xs:element
			}

			while (_complexTypeToBuild.Count > 0)
			{
				Type toBuild = _complexTypeToBuild.Dequeue();
				if (!_builtComplexTypes.Contains(toBuild.Name))
				{
					writer.WriteStartElement("xs:complexType");
					if (toBuild.IsArray)
					{
						writer.WriteAttributeString("name", "ArrayOf" + toBuild.Name.Replace("[]", string.Empty));
					}
					else
					{
						writer.WriteAttributeString("name", toBuild.Name);
					}
					writer.WriteStartElement("xs:sequence");

					if (toBuild.IsArray)
					{
						var elementType = toBuild.GetElementType();
						AddSchemaType(writer, elementType, null, true);
					}
					else if (typeof(IEnumerable).IsAssignableFrom(toBuild))
					{

						// Recursively look through the base class to find the Generic Type of the Enumerable
						var baseType = toBuild;
						var baseTypeInfo = toBuild.GetTypeInfo();
						while (!baseTypeInfo.IsGenericType && baseTypeInfo.BaseType != null)
						{
							baseType = baseTypeInfo.BaseType;
							baseTypeInfo = baseType.GetTypeInfo();
						}
						var generic = baseType.GetTypeInfo().GetGenericArguments().DefaultIfEmpty(typeof(object)).FirstOrDefault();
						AddSchemaType(writer, generic, null, true);
					}
					else
					{
						foreach (var property in toBuild.GetProperties())
						{
							AddSchemaType(writer, property.PropertyType, property.Name);
						}
					}

					writer.WriteEndElement(); // xs:sequence
					writer.WriteEndElement(); // xs:complexType

					_builtComplexTypes.Add(toBuild.Name);
				}
			}

			while (_enumToBuild.Count > 0)
			{
				Type toBuild = _enumToBuild.Dequeue();
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

		private void AddSchemaType(XmlDictionaryWriter writer, Type type, string name, bool isArray = false, string @namespace = null)
		{
			var typeInfo = type.GetTypeInfo();
			writer.WriteStartElement("xs:element");

			// Check for null, since we may use empty NS
			if (@namespace != null)
			{
				writer.WriteAttributeString("targetNamespace", @namespace);
			}
			
			if (typeInfo.IsValueType)
			{
				string xsTypename;
				if (typeInfo.IsEnum)
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
				else if (type.Name == "Byte[]")
				{
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
					writer.WriteAttributeString("type", "tns:ArrayOf" + type.Name.Replace("[]", string.Empty));

					_complexTypeToBuild.Enqueue(type);
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
			}

			if (String.IsNullOrEmpty(resolvedType))
			{
				throw new ArgumentException($".NET type {typeName} cannot be resolved into XML schema type");
			}

			return resolvedType;
		}

	}

}
