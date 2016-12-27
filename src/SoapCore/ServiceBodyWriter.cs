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
		private readonly string _serviceNamespace;
		private readonly string _envelopeName;
		private readonly string _resultName;
		private readonly object _result;
		private readonly Dictionary<string, object> _outResults;

		public ServiceBodyWriter(string serviceNamespace, string envelopeName, string resultName, object result, Dictionary<string, object> outResults) : base(isBuffered: true)
		{
			_serviceNamespace = serviceNamespace;
			_envelopeName = envelopeName;
			_resultName = resultName;
			_result = result;
			_outResults = outResults;
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement(_envelopeName, _serviceNamespace);

			if (_outResults != null)
			{
				foreach (var outResult in _outResults)
				{
					string value;
					if (outResult.Value is Guid)
						value = outResult.Value.ToString();
					else if (outResult.Value is bool)
						value = outResult.Value.ToString().ToLower();
					else if (outResult.Value is string)
						value = SecurityElement.Escape(outResult.Value.ToString());
					else if (outResult.Value is Enum)
						value = outResult.Value.ToString();
					else //for complex types
					{
						using (var ms = new MemoryStream())
						using (BufferedStream stream = new BufferedStream(ms))
						{
							new XmlSerializer(outResult.Value.GetType()).Serialize(ms, outResult.Value);
							stream.Position = 0;
							using (var reader = XmlReader.Create(stream))
							{
								reader.MoveToContent();
								value = reader.ReadInnerXml();
							}
						}
					}

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
