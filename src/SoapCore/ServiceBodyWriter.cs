using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	public class ServiceBodyWriter : BodyWriter
	{
		private readonly SoapSerializer _serializer;
		private readonly string _serviceNamespace;
		private readonly string _envelopeName;
		private readonly string _resultName;
		private readonly object _result;
		private readonly Dictionary<string, object> _outResults;

		public ServiceBodyWriter(SoapSerializer serializer, string serviceNamespace, string envelopeName, string resultName, object result, Dictionary<string, object> outResults) : base(isBuffered: true)
		{
			_serializer = serializer;
			_serviceNamespace = serviceNamespace;
			_envelopeName = envelopeName;
			_resultName = resultName;
			_result = result;
			_outResults = outResults;
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			// do not wrap old-style single element response into additional xml element for xml serializer
			var needResponseEnvelope = _serializer != SoapSerializer.XmlSerializer || _result == null || (_outResults != null && _outResults.Count > 0);

			if (needResponseEnvelope)
				writer.WriteStartElement(_envelopeName, _serviceNamespace);

			if (_outResults != null)
			{
				foreach (var outResult in _outResults)
				{
					string value = null;
					if (outResult.Value is Guid)
						value = outResult.Value.ToString();
					else if (outResult.Value is bool)
						value = outResult.Value.ToString().ToLower();
					else if (outResult.Value is string)
						value = SecurityElement.Escape(outResult.Value.ToString());
					else if (outResult.Value is Enum)
						value = outResult.Value.ToString();
					else if (outResult.Value == null)
						value = null;
					else //for complex types
					{
						using (var ms = new MemoryStream())
						using (var stream = new BufferedStream(ms))
						{
							switch (_serializer)
							{
								case SoapSerializer.XmlSerializer:
									// todo: write element with outResult.Key name and type information outResultType
									// i.e. <outResult.Key xsi:type="outResultType" ... />
									// upd: custom element name is ok, missing type info
									var outResultType = outResult.Value.GetType();
									var serializer = CachedXmlSerializer.GetXmlSerializer(outResultType, outResult.Key, _serviceNamespace);
									lock (serializer)
										serializer.Serialize(writer, outResult.Value);
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
						writer.WriteRaw(string.Format("<{0}>{1}</{0}>", outResult.Key, value));
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
							var serializer = CachedXmlSerializer.GetXmlSerializer(resultType, needResponseEnvelope ? _resultName : resultType.Name, _serviceNamespace);
							lock (serializer)
								serializer.Serialize(writer, _result);
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
				writer.WriteEndElement();
		}
	}
}
