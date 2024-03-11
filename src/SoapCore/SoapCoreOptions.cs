using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Xml;
using SoapCore.Extensibility;

namespace SoapCore
{
	public class SoapCoreOptions
	{
		private bool? _indentWsdl = null;

#if NET8_0_OR_GREATER
		/// <summary>
		/// Gets or sets the Path of the Service
		/// </summary>
		required public string Path { get; set; }
#else
		/// <summary>
		/// Gets or sets the Path of the Service
		/// </summary>
		public string Path { get; set; }
#endif
		/// <summary>
		/// Gets or sets encoders
		/// </summary>
		public SoapEncoderOptions[] EncoderOptions { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the kind of serializer
		/// <para>Defaults to <see cref="SoapSerializer.DataContractSerializer"/></para>
		/// </summary>
		public SoapSerializer SoapSerializer { get; set; } = SoapSerializer.DataContractSerializer;

		/// <summary>
		/// Gets or sets a value indicating whether Path is case-sensitive
		/// <para>Defaults to false</para>
		/// </summary>
		public bool CaseInsensitivePath { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating the ModelBounder
		/// <para>Defaults to null</para>
		/// </summary>
		public ISoapModelBounder SoapModelBounder { get; set; } = null;

		/// <summary>
		/// Gets or sets a value whether to use basic authentication
		/// <para>Defaults to false</para>
		/// </summary>
		public bool UseBasicAuthentication { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether publication of service metadata on HTTP GET request is activated
		/// <para>Defaults to true</para>
		/// </summary>
		public bool HttpGetEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether publication of service metadata on HTTPS GET request is activated
		/// <para>Defaults to true</para>
		/// </summary>
		public bool HttpsGetEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether invocation by posting formdata on HTTP is activated
		/// <para>Defaults to true</para>
		/// </summary>
		public bool HttpPostEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether invocation by posting formdata on HTTP is activated
		/// <para>Defaults to true</para>
		/// </summary>
		public bool HttpsPostEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether to omit the Xml declaration (&lt;?xml version="1.0" encoding="utf-8"?&gt;) in responses
		/// <para>Defaults to true</para>
		/// </summary>
		public bool OmitXmlDeclaration { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating wheter to add the stand alone attribute in the XML declaration
		/// <para>Defaults to false</para>
		/// </summary>
		public bool? StandAloneAttribute { get; set; } = null;

		/// <summary>
		/// Gets or sets a value indicating whether to indent the Xml in responses
		/// <para>Defaults to true</para>
		/// </summary>
		public bool IndentXml { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether to indent the generated WSDL.
		/// <para>Defaults to the value of <see cref="IndentXml"/></para>
		/// </summary>
		public bool IndentWsdl { get => _indentWsdl ?? IndentXml; set => _indentWsdl = value; }

		/// <summary>
		/// Gets or sets a value indicating whether to check to make sure that the XmlOutput doesn't contain invalid characters
		/// <para>Defaults to true</para>
		/// </summary>
		public bool CheckXmlCharacters { get; set; } = true;

		/// <summary>
		/// Add Microsoft Guid schema to wsdl
		/// </summary>
		public bool UseMicrosoftGuid { get; set; } = false;

		/// <summary>
		/// Gets or sets an collection of Xml Namespaces to override the default prefix for.
		/// </summary>
		public XmlNamespaceManager XmlNamespacePrefixOverrides { get; set; }

		public WsdlFileOptions WsdlFileOptions { get; set; }


		/// <summary>
		/// Sets additional namespace declaration attributes in envelope
		/// </summary>
		public Dictionary<string, string> AdditionalEnvelopeXmlnsAttributes { get; set; }

		/// <summary>
		/// By default, the soapAction that is generated if not explicitely specified is
		/// {namespace}/{contractName}/{methodName}. If set to true, the service name will
		/// be omitted, so that the soapAction will be {namespace}/{methodName}.
		/// </summary>
		public bool GenerateSoapActionWithoutContractName { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether newlines in the SOAP XML responses
		/// should be normalized to the system's default newline character (CRLF on Windows).
		/// Default is true.
		/// </summary>
		public bool NormalizeNewLines { get; set; } = true;
	}
}
