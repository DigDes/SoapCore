using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapCore
{
	public class FaultBodyWriter : BodyWriter
	{
		private readonly MessageVersion _version;
		private readonly Exception _exception;
		private readonly string _faultStringOverride;

		public FaultBodyWriter(Exception exception, MessageVersion version, bool isBuffered = true, string faultStringOverride = null) : base(isBuffered)
		{
			_version = version;
			_exception = exception;
			_faultStringOverride = faultStringOverride;
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			if (_version.Envelope == EnvelopeVersion.Soap12)
			{
				WriteSoap12Fault(writer);
			}
			else if (_version.Envelope == EnvelopeVersion.Soap11)
			{
				WriteSoap11Fault(writer);
			}
			else
			{
				// We will default to the oldest SOAP format instead of
				// breaking everything
				WriteSoap11Fault(writer);
			}
		}

		private void WriteSoap12Fault(XmlDictionaryWriter writer)
		{
			// NOTE: This default culture is a hack until a better localisation solution is
			// built. At this stage it assumes the current thread culture
			var defaultCulture = CultureInfo.CurrentCulture;

			var faultString = _faultStringOverride ?? (_exception.InnerException != null ? _exception.InnerException.Message : _exception.Message);
			var faultDetail = ExtractFaultDetailsAsXmlElement(_exception);
			var prefix = writer.LookupPrefix(Namespaces.SOAP12_ENVELOPE_NS) ?? "s";

			writer.WriteStartElement(prefix, "Fault", Namespaces.SOAP12_ENVELOPE_NS);

			writer.WriteStartElement(prefix, "Code", Namespaces.SOAP12_ENVELOPE_NS);
			writer.WriteStartElement(prefix, "Value", Namespaces.SOAP12_ENVELOPE_NS);
			writer.WriteString(prefix + ":Sender");
			writer.WriteEndElement();
			writer.WriteEndElement();

			writer.WriteStartElement(prefix, "Reason", Namespaces.SOAP12_ENVELOPE_NS);
			writer.WriteStartElement(prefix, "Text", Namespaces.SOAP12_ENVELOPE_NS);

			writer.WriteAttributeString("xml", "lang", null, defaultCulture.IetfLanguageTag);
			writer.WriteString(faultString);

			writer.WriteEndElement();
			writer.WriteEndElement();

			if (faultDetail != null)
			{
				writer.WriteStartElement(prefix, "Detail", Namespaces.SOAP12_ENVELOPE_NS);
				faultDetail.WriteTo(writer);
				writer.WriteEndElement();
			}

			writer.WriteEndElement();
		}

		private void WriteSoap11Fault(XmlDictionaryWriter writer)
		{
			var faultString = _faultStringOverride ?? (_exception.InnerException != null ? _exception.InnerException.Message : _exception.Message);
			var faultDetail = ExtractFaultDetailsAsXmlElement(_exception);

			writer.WriteStartElement("Fault", Namespaces.SOAP11_ENVELOPE_NS);

			/* SUPPORT FOR SPECIFYING CUSTOM FAULTCODE AND NAMESPACE

			For Example, this would result in the response below:
			throw new System.ServiceModel.FaultException(new FaultReason("faultString1"), new FaultCode("faultCode1", "faultNamespace1"), "action1");
			<s:Envelope xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
				<s:Body>
					<s:Fault>
						<a:faultcode xmlns:a="faultNamespace1">a:faultCode1</a:faultcode>
						<faultstring>faultString1</faultstring>
					</s:Fault>
				</s:Body>
			</s:Envelope>

			For Example, this would result in the response below:
			throw new System.ServiceModel.FaultException(new FaultReason("faultString1"), new FaultCode("faultCode1"), "action1");
			<s:Envelope xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
				<s:Body>
					<s:Fault>
						<faultcode>s:faultCode1</faultcode>
						<faultstring>faultString1</faultstring>
					</s:Fault>
				</s:Body>
			</s:Envelope>
			*/
			if (_exception is FaultException)
			{
				var faultException = (FaultException)_exception;
				if (faultException != null && faultException.Code != null && !string.IsNullOrEmpty(faultException.Code.Name))
				{
					if (!string.IsNullOrEmpty(faultException.Code.Namespace))
					{
						writer.WriteElementString("a", "faultcode", faultException.Code.Namespace, "a:" + faultException.Code.Name);
					}
					else
					{
						writer.WriteElementString("faultcode", "s:" + faultException.Code.Name);
					}
				}
				else
				{
					writer.WriteElementString("faultcode", "s:Client");
				}
			}
			else
			{
				writer.WriteElementString("faultcode", "s:Client");
			}

			writer.WriteElementString("faultstring", faultString);

			if (faultDetail != null)
			{
				writer.WriteStartElement("detail");
				faultDetail.WriteTo(writer);
				writer.WriteEndElement();
			}

			writer.WriteEndElement();
		}

		private XmlElement ExtractFaultDetailsAsXmlElement(Exception ex)
		{
			var detailObject = ExtractFaultDetail(ex);

			if (detailObject == null)
			{
				return null;
			}

			using (var ms = new MemoryStream())
			{
				var serializer = new DataContractSerializer(detailObject.GetType());
				serializer.WriteObject(ms, detailObject);
				ms.Position = 0;
				var doc = new XmlDocument();
				doc.Load(ms);
				return doc.DocumentElement;
			}
		}

		/// <summary>
		/// Helper to extract object of a detailed fault.
		/// </summary>
		/// <param name="exception">
		/// The exception that caused the failure.
		/// </param>
		/// <returns>
		/// Returns instance of T if the exception (or its InnerExceptions) is of type FaultException<T>.
		/// otherwise returns null
		/// </returns>
		private object ExtractFaultDetail(Exception exception)
		{
			try
			{
				var currentException = exception;

				while (currentException != null)
				{
					var type = currentException.GetType();

					if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(FaultException<>))
					{
						var detailInfo = type.GetProperty("Detail");
						var value = detailInfo?.GetValue(currentException);

						if (value != null)
						{
							return value;
						}
					}

					currentException = currentException.InnerException;
				}
			}
			catch
			{
				return null;
			}

			return null;
		}
	}
}
