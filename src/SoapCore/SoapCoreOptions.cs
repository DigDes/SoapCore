using System.ServiceModel.Channels;
using SoapCore.Extensibility;

namespace SoapCore
{
	public class SoapCoreOptions
	{
		/// <summary>
		/// Gets or sets the Path of the Service
		/// </summary>
		public string Path { get; set; }

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
		/// Gets or sets a value indicating the binding to use
		/// <para>Defaults to null</para>
		/// </summary>
		public Binding Binding { get; set; }

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
		/// The maximum size in bytes of the in-memory <see cref="System.Buffers.ArrayPool{Byte}"/> used to buffer the
		/// stream. Larger request bodies are written to disk.
		/// </summary>
		public int BufferThreshold { get; set; } = 1024 * 30;

		/// <summary>
		/// The maximum size in bytes of the request body. An attempt to read beyond this limit will cause an
		/// <see cref="System.IO.IOException"/>.
		/// </summary>
		public long BufferLimit { get; set; }
	}
}
