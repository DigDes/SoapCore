using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.ObjectPool;

namespace SoapCore
{
	public class Fault
	{
		[XmlElement(ElementName = "faultcode")]
		public string FaultCode { get; set; }

		[XmlElement(ElementName = "faultstring")]
		public string FaultString { get; set; }

		[XmlElement(ElementName = "detail")]
		public XmlElement Details { get; set; }

		public Fault()
		{
			FaultCode = "s:Client";
		}

		public Fault(object detailObject) : this()
		{
			Details = SerializeDetails(detailObject);
		}

		private XmlElement SerializeDetails(object detailObject)
		{
			if (detailObject == null) return null;

			XmlDocument doc = new XmlDocument();
			using (XmlWriter writer = doc.CreateNavigator().AppendChild())
			{
				new XmlSerializer(detailObject.GetType()).Serialize(writer, detailObject);
			}

			return doc.DocumentElement;
		}
	}
}
