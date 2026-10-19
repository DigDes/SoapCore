using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using SoapCore.Extensibility;

namespace SoapCore
{
	/// <summary>
	/// Point-in-time description of one SOAP endpoint configured with <c>UseSoapEndpoint</c>.
	/// SoapCore creates a new instance per call and never changes it after passing it to
	/// <see cref="ISoapEndpointRegistry.Register(SoapEndpointInfo)"/>. Treat it as read-only.
	/// </summary>
	public class SoapEndpointInfo
	{
		/// <summary>
		/// Gets or sets the path exactly as configured in <see cref="SoapCoreOptions.Path"/>. It is not normalized.
		/// <para>Prefixes added around the endpoint (<c>app.Map</c>, <c>UsePathBase</c>) are not included.</para>
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// Gets or sets the service type passed to <c>UseSoapEndpoint</c>.
		/// </summary>
		public Type ServiceType { get; set; }

		/// <summary>
		/// Gets or sets the message type (<c>T_MESSAGE</c>) used by the endpoint.
		/// <para>Defaults to <see cref="CustomMessage"/>.</para>
		/// </summary>
		public Type MessageType { get; set; } = typeof(CustomMessage);

		/// <summary>
		/// Gets or sets the configured serializer. It selects the WSDL writer and, unless a custom
		/// serializer is in effect (see <see cref="CustomSerializerType"/>), the message serializer.
		/// <para>Defaults to DataContractSerializer.</para>
		/// </summary>
		public SoapSerializer Serializer { get; set; } = SoapSerializer.DataContractSerializer;

		/// <summary>
		/// Gets or sets the handler type selected with <see cref="SoapCoreOptions.UseCustomSerializer{TCustomSerializer}"/>, or null.
		/// <para>SoapCore only uses it when the serializer resolver registered by <c>AddSoapCore()</c> is present.</para>
		/// </summary>
		public Type CustomSerializerType { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="Path"/> is matched case-insensitively.
		/// </summary>
		public bool CaseInsensitivePath { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether generated SOAPAction values omit the contract name.
		/// <para>Pass it to <c>new ServiceDescription(ServiceType, GenerateSoapActionWithoutContractName)</c> to get the same SOAPActions as the endpoint.</para>
		/// </summary>
		public bool GenerateSoapActionWithoutContractName { get; set; }

		/// <summary>
		/// Gets or sets the distinct message versions (SOAP envelope and WS-Addressing) of the endpoint's encoders, in configuration order.
		/// <para>When no encoder is configured, this is SoapCore's default encoder version (SOAP 1.1).</para>
		/// </summary>
		public IReadOnlyList<MessageVersion> MessageVersions { get; set; } = Array.Empty<MessageVersion>();

		internal static SoapEndpointInfo FromSoapOptions(SoapOptions options, Type messageType)
		{
			// Same default the middleware applies (SoapEndpointMiddleware: EncoderOptions ??= ...), without mutating the options.
			var encoderOptions = options.EncoderOptions ?? SoapEncoderOptions.CreateDefaults();

			return new SoapEndpointInfo
			{
				Path = options.Path,
				ServiceType = options.ServiceType,
				MessageType = messageType,
				Serializer = options.SoapSerializer,
				CustomSerializerType = options.SerializerIdentifier,
				CaseInsensitivePath = options.CaseInsensitivePath,
				GenerateSoapActionWithoutContractName = options.GenerateSoapActionWithoutContractName,

				// Null guards: in middleware mode this runs before the middleware validates the configuration,
				// so it must never throw for a configuration that does not throw today.
				MessageVersions = Array.AsReadOnly(encoderOptions
					.Where(encoder => encoder?.MessageVersion != null)
					.Select(encoder => encoder.MessageVersion)
					.Distinct()
					.ToArray()),
			};
		}
	}
}
