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
using SoapCore.ServiceModel;

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

		private static void WriteStream(XmlDictionaryWriter writer, object value)
		{
			int blockSize = 256;
			int bytesRead = 0;
			byte[] block = new byte[blockSize];
			var stream = (Stream)value;
			stream.Position = 0;

			while (true)
			{
				bytesRead = stream.Read(block, 0, blockSize);
				if (bytesRead > 0)
				{
					writer.WriteBase64(block, 0, bytesRead);
				}
				else
				{
					break;
				}

				if (blockSize < 65536 && bytesRead == blockSize)
				{
					blockSize = blockSize * 16;
					block = new byte[blockSize];
				}
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
				var messageContractAttribute = resultType.GetTypeInfo().GetCustomAttribute<MessageContractAttribute>();

				var xmlName = _operation.ReturnElementName
					?? (needResponseEnvelope
					? _resultName
					: (string.IsNullOrWhiteSpace(xmlRootAttr?.ElementName)
					? resultType.Name
					: xmlRootAttr.ElementName));

				var xmlNs = _operation.ReturnNamespace ?? messageContractAttribute?.WrapperNamespace
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
					// This behavior is opt-in i.e. you have to explicitly have a [MessageContract(IsWrapped=false)]
					// to have the message body members inlined.
					var shouldInline = (messageContractAttribute != null && messageContractAttribute.IsWrapped == false) || resultType.GetMembersWithAttribute<MessageHeaderAttribute>().Any();

					if (shouldInline)
					{
						var memberInformation = resultType.GetMembersWithAttribute<MessageBodyMemberAttribute>().Select(mi => new
						{
							Member = mi,
							MessageBodyMemberAttribute = mi.GetCustomAttribute<MessageBodyMemberAttribute>()
						}).OrderBy(x => x.MessageBodyMemberAttribute?.Order ?? 0);

						if (messageContractAttribute != null && messageContractAttribute.IsWrapped)
						{
							writer.WriteStartElement(resultType.Name, xmlNs);
						}

						foreach (var memberInfo in memberInformation)
						{
							var memberType = memberInfo.Member.GetPropertyOrFieldType();
							var memberValue = memberInfo.Member.GetPropertyOrFieldValue(_result);

							var memberName = memberInfo.MessageBodyMemberAttribute?.Name ?? memberInfo.Member.Name;
							var memberNamespace = memberInfo.MessageBodyMemberAttribute?.Namespace ?? _serviceNamespace;

							var serializer = CachedXmlSerializer.GetXmlSerializer(memberType, memberName, memberNamespace);

							lock (serializer)
							{
								if (memberValue is Stream)
								{
									writer.WriteStartElement(memberName, _serviceNamespace);

									WriteStream(writer, memberValue);

									writer.WriteEndElement();
								}
								else
								{
									serializer.Serialize(writer, memberValue);
								}
							}
						}

						if (messageContractAttribute != null && messageContractAttribute.IsWrapped)
						{
							writer.WriteEndElement();
						}
					}
					else
					{
						var serializer = CachedXmlSerializer.GetXmlSerializer(resultType, xmlName, xmlNs);

						lock (serializer)
						{
							if (_result is Stream)
							{
								writer.WriteStartElement(_resultName, _serviceNamespace);
								WriteStream(writer, _result);
								writer.WriteEndElement();
							}
							else
							{
								serializer.Serialize(writer, _result);
							}
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
				if (_result is Stream)
				{
					writer.WriteStartElement(_resultName, _serviceNamespace);
					WriteStream(writer, _result);
					writer.WriteEndElement();
				}
				else
				{
					var serializer = new DataContractSerializer(_result.GetType(), _resultName, _serviceNamespace);
					serializer.WriteObject(writer, _result);
				}
			}

			writer.WriteEndElement();
		}
	}
}
