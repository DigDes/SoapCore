using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Xml;
using SoapCore.Extensibility;
using SoapCore.Meta;

namespace SoapCore
{
	public class SoapOptions
	{
		public Type ServiceType { get; set; }
		public string Path { get; set; }
		public SoapEncoderOptions[] EncoderOptions { get; set; }
		public SoapSerializer SoapSerializer { get; set; }
		public bool CaseInsensitivePath { get; set; }
		public ISoapModelBounder SoapModelBounder { get; set; }

		[Obsolete]
		public Binding Binding { get; set; }

		public bool UseBasicAuthentication { get; set; }

		[Obsolete]
		public int BufferThreshold { get; set; }
		[Obsolete]
		public long BufferLimit { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether publication of service metadata on HTTP GET request, and invocation of service operation by GET, is activated
		/// <para>Defaults to true</para>
		/// </summary>
		public bool HttpGetEnabled { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether publication of service metadata on HTTPS GET request, and invocation of service operation by GET, is activated
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

		public bool OmitXmlDeclaration { get; set; } = true;

		public bool IndentXml { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether to check to make sure that the XmlOutput doesn't contain invalid characters
		/// <para>Defaults to true</para>
		/// </summary>
		public bool CheckXmlCharacters { get; set; } = true;

		public XmlNamespaceManager XmlNamespacePrefixOverrides { get; set; }
		public WsdlFileOptions WsdlFileOptions { get; set; }
		public Dictionary<string, string> AdditionalEnvelopeXmlnsAttributes { get; set; }

		[Obsolete]
		public static SoapOptions FromSoapCoreOptions<T>(SoapCoreOptions opt)
		{
			return FromSoapCoreOptions(opt, typeof(T));
		}

		public static SoapOptions FromSoapCoreOptions(SoapCoreOptions opt, Type serviceType)
		{
			var options = new SoapOptions
			{
				ServiceType = serviceType,
				Path = opt.Path,
				EncoderOptions = opt.EncoderOptions,
				SoapSerializer = opt.SoapSerializer,
				CaseInsensitivePath = opt.CaseInsensitivePath,
				SoapModelBounder = opt.SoapModelBounder,
				UseBasicAuthentication = opt.UseBasicAuthentication,
				HttpsGetEnabled = opt.HttpsGetEnabled,
				HttpGetEnabled = opt.HttpGetEnabled,
				HttpPostEnabled = opt.HttpPostEnabled,
				HttpsPostEnabled = opt.HttpsPostEnabled,
				OmitXmlDeclaration = opt.OmitXmlDeclaration,
				IndentXml = opt.IndentXml,
				XmlNamespacePrefixOverrides = opt.XmlNamespacePrefixOverrides,
				WsdlFileOptions = opt.WsdlFileOptions,
				AdditionalEnvelopeXmlnsAttributes = opt.AdditionalEnvelopeXmlnsAttributes,
				CheckXmlCharacters = opt.CheckXmlCharacters
			};

#pragma warning disable CS0612 // Type or member is obsolete
			if (opt.Binding is object)
			{
				if (opt.Binding.HasBasicAuth())
				{
					options.UseBasicAuthentication = true;
				}

				if (options.EncoderOptions is null)
				{
					opt.EncoderOptions = opt.Binding.ToEncoderOptions();
				}
			}
#pragma warning restore CS0612 // Type or member is obsolete

			return options;
		}
	}
}
