using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	public class ServiceBodyWriter : BodyWriter
	{
		private readonly SoapSerializer _serializer;
		private readonly OperationDescription _operation;
		private readonly string _serviceNamespace;
		private readonly string _envelopeName;
		private readonly string _resultName;
		private readonly object _result;
		private readonly Dictionary<string, object> _outResults;

		public ServiceBodyWriter(SoapSerializer serializer, OperationDescription operation, string resultName, object result, Dictionary<string, object> outResults) : base(isBuffered: true)
		{
			_serializer = serializer;
			_operation = operation;
			_serviceNamespace = operation.Contract.Namespace;
			_envelopeName = operation.Name + "Response";
			_resultName = resultName;
			_result = result;
			_outResults = outResults;
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			// do not wrap old-style single element response into additional xml element for xml serializer
			var needResponseEnvelope = _serializer != SoapSerializer.XmlSerializer || _result == null || (_outResults != null && _outResults.Count > 0) || !_operation.IsMessageContractResponse;

			if (needResponseEnvelope)
			{
				writer.WriteStartElement(_envelopeName, _serviceNamespace);
			}

			if (_outResults != null)
			{
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

					//for complex types
					else
					{
						using (var ms = new MemoryStream())
						using (var stream = new BufferedStream(ms))
						{
							switch (_serializer)
							{
								case SoapSerializer.XmlSerializer:
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
									break;
								case SoapSerializer.DataContractSerializer:
									new DataContractSerializer(outResult.Value.GetType()).WriteObject(ms, outResult.Value);
									stream.Position = 0;
									using (var reader = XmlReader.Create(stream))
									{
										reader.MoveToContent();
										value = reader.ReadInnerXml();
									}

									break;
								default: throw new NotImplementedException();
							}
						}
					}

					if (value != null)
					{
						writer.WriteRaw(string.Format("<{0}>{1}</{0}>", outResult.Key, value));
					}
				}
			}

			if (_result != null)
			{
				switch (_serializer)
				{
					case SoapSerializer.XmlSerializer:
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
								var serializer = CachedXmlSerializer.GetXmlSerializer(resultType, xmlName, xmlNs);
								lock (serializer)
								{
									serializer.Serialize(writer, _result);
								}
							}
						}

						break;
					case SoapSerializer.DataContractSerializer:
						{
							var serializer = new DataContractSerializer(_result.GetType(), _resultName, _serviceNamespace);
							serializer.WriteObject(writer, _result);
						}

						break;
					default: throw new NotImplementedException();
				}
			}

			if (needResponseEnvelope)
			{
				writer.WriteEndElement();
			}
		}
	}
}
