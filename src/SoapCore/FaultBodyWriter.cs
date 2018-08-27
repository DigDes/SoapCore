using System.IO;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	public class FaultBodyWriter : BodyWriter
	{
		private readonly Fault _fault;

		public FaultBodyWriter(Fault fault, bool isBuffered = true) : base(isBuffered)
		{
			_fault = fault;
		}

		protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
		{
			writer.WriteStartElement("Fault", "http://schemas.xmlsoap.org/soap/envelope/");
			using (var ms = new MemoryStream())
			using (var stream = new BufferedStream(ms))
			{
				new XmlSerializer(_fault.GetType()).Serialize(ms, _fault);
				stream.Position = 0;
				using (var reader = XmlReader.Create(stream))
				{
					reader.MoveToContent();
					var value = reader.ReadInnerXml();
					writer.WriteRaw(value);
				}
			}

			writer.WriteEndElement();
		}
	}
}
