using System.IO;
using System.Runtime.Serialization;
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
	}
}
