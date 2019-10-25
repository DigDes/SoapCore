using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	internal class ServiceBodyWriter : BodyWriter
	{
		private readonly SoapSerializer _serializer;
		private readonly OperationDescription _operation;
		private readonly string _serviceNamespace;
		private readonly string _envelopeName;
		private readonly string _resultName;
		private readonly object _result;
		private readonly Dictionary<string, object> _outResults;

		public ServiceBodyWriter(SoapSerializer serializer, OperationDescription operation, object result, Dictionary<string, object> outResults) : base(isBuffered: true)
		{
			_serializer = serializer;
			_operation = operation;
			_serviceNamespace = operation.Contract.Namespace;
			_envelopeName = operation.Name + "Response";
			_resultName = operation.ReturnName;
			_result = result;
			_outResults = outResults ?? new Dictionary<string, object>();
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			switch (_serializer)
			{
				case SoapSerializer.XmlSerializer:
					OnWriteXmlSerializerBodyContents(writer);
					break;
				case SoapSerializer.DataContractSerializer:
					OnWriteDataContractSerializerBodyContents(writer);
					break;
				default:
					throw new NotImplementedException($"Unknown serializer {_serializer}");
			}
		}

		private void OnWriteXmlSerializerBodyContents(XmlDictionaryWriter writer)
		{
			Debug.Assert(_outResults != null, "Object should set empty out results");

			// Do not wrap old-style single element response into additional xml element for xml serializer
			var needResponseEnvelope = _result == null || (_outResults.Count > 0) || !_operation.IsMessageContractResponse;

			if (needResponseEnvelope)
			{
				writer.WriteStartElement(_envelopeName, _serviceNamespace);
			}

			foreach (var outResult in _outResults)
			{
				string value = null;
				if (outResult.Value is Guid)
				{
					value = outResult.Value.ToString();
				}
				else if (outResult.Value is bool)
				{
					value = outResult.Value.ToString().ToLower();
				}
				else if (outResult.Value is string)
				{
					value = System.Security.SecurityElement.Escape(outResult.Value.ToString());
				}
				else if (outResult.Value is Enum)
				{
					value = outResult.Value.ToString();
				}
				else if (outResult.Value == null)
				{
					value = null;
				}
				else
				{
					//for complex types
					using (var ms = new MemoryStream())
					using (var stream = new BufferedStream(ms))
					{
						// write element with name as outResult.Key and type information as outResultType
						// i.e. <outResult.Key xsi:type="outResultType" ... />
						var outResultType = outResult.Value.GetType();
						var serializer = CachedXmlSerializer.GetXmlSerializer(outResultType, outResult.Key, _serviceNamespace);
						lock (serializer)
						{
							serializer.Serialize(stream, outResult.Value);
						}

						//add outResultType. ugly, but working
						stream.Position = 0;
						XmlDocument xdoc = new XmlDocument();
						xdoc.Load(stream);
						var attr = xdoc.CreateAttribute("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance");
						attr.Value = outResultType.Name;
						xdoc.DocumentElement.Attributes.Prepend(attr);
						writer.WriteRaw(xdoc.DocumentElement.OuterXml);
					}
				}

				if (value != null)
				{
					writer.WriteRaw(string.Format("<{0}>{1}</{0}>", outResult.Key, value));
				}
			}

			if (_result != null)
			{
				// see https://referencesource.microsoft.com/System.Xml/System/Xml/Serialization/XmlSerializer.cs.html#c97688a6c07294d5
				var resultType = _result.GetType();

				var xmlRootAttr = resultType.GetTypeInfo().GetCustomAttributes<XmlRootAttribute>().FirstOrDefault();

				var xmlName = _operation.ReturnElementName
					?? (needResponseEnvelope
					? _resultName
					: (string.IsNullOrWhiteSpace(xmlRootAttr?.ElementName)
					? resultType.Name
					: xmlRootAttr.ElementName));

				var xmlNs = _operation.ReturnNamespace
					?? (string.IsNullOrWhiteSpace(xmlRootAttr?.Namespace)
					? _serviceNamespace
					: xmlRootAttr.Namespace);

				var xmlArrayAttr = _operation.DispatchMethod.GetCustomAttribute<XmlArrayAttribute>();

				if (xmlArrayAttr != null && resultType.IsArray)
				{
					var serializer = CachedXmlSerializer.GetXmlSerializer(resultType.GetElementType(), xmlName, xmlNs);

					lock (serializer)
					{
						serializer.SerializeArray(writer, (object[])_result);
					}
				}
				else
				{
					var messageContractAttribute = resultType.GetCustomAttribute<MessageContractAttribute>();

					// This behavior is opt-in i.e. you have to explicitly have a [MessageContract(IsWrapped=false)]
					// to have the message body members inlined.
					var shouldInline = messageContractAttribute != null && messageContractAttribute.IsWrapped == false;

					if (shouldInline)
					{
						var memberInformation = resultType
							.GetPropertyOrFieldMembers()
							.Select(mi => new
							{
								Member = mi,
								MessageBodyMemberAttribute = mi.GetCustomAttribute<MessageBodyMemberAttribute>()
							})
							.OrderBy(x => x.MessageBodyMemberAttribute?.Order ?? 0);

						foreach (var memberInfo in memberInformation)
						{
							var memberType = memberInfo.Member.GetPropertyOrFieldType();
							var memberValue = memberInfo.Member.GetPropertyOrFieldValue(_result);

							var memberName = memberInfo.MessageBodyMemberAttribute?.Name ?? memberInfo.Member.Name;
							var memberNamespace = memberInfo.MessageBodyMemberAttribute?.Namespace ?? xmlNs;

							var serializer = CachedXmlSerializer.GetXmlSerializer(memberType, memberName, memberNamespace);

							lock (serializer)
							{
								serializer.Serialize(writer, memberValue);
							}
						}
					}
					else
					{
						var serializer = CachedXmlSerializer.GetXmlSerializer(resultType, xmlName, xmlNs);

						lock (serializer)
						{
							serializer.Serialize(writer, _result);
						}
					}
				}
			}

			if (needResponseEnvelope)
			{
				writer.WriteEndElement();
			}
		}

		private void OnWriteDataContractSerializerBodyContents(XmlDictionaryWriter writer)
		{
			Debug.Assert(_outResults != null, "Object should set empty out results");

			writer.WriteStartElement(_envelopeName, _serviceNamespace);

			foreach (var outResult in _outResults)
			{
				string value = null;

				if (outResult.Value is Guid)
				{
					value = outResult.Value.ToString();
				}
				else if (outResult.Value is bool)
				{
					value = outResult.Value.ToString().ToLower();
				}
				else if (outResult.Value is string)
				{
					value = System.Security.SecurityElement.Escape(outResult.Value.ToString());
				}
				else if (outResult.Value is Enum)
				{
					value = outResult.Value.ToString();
				}
				else if (outResult.Value == null)
				{
					value = null;
				}
				else
				{
					//for complex types
					using (var ms = new MemoryStream())
					using (var stream = new BufferedStream(ms))
					{
						new DataContractSerializer(outResult.Value.GetType()).WriteObject(ms, outResult.Value);
						stream.Position = 0;
						using (var reader = XmlReader.Create(stream))
						{
							reader.MoveToContent();
							value = reader.ReadInnerXml();
						}
					}
				}

				if (value != null)
				{
					writer.WriteRaw(string.Format("<{0}>{1}</{0}>", outResult.Key, value));
				}
			}

			if (_result != null)
			{
				var serializer = new DataContractSerializer(_result.GetType(), _resultName, _serviceNamespace);
				serializer.WriteObject(writer, _result);
			}

			writer.WriteEndElement();
		}
	}
}
