using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[XmlSchemaProvider("MyCustomSchemaProvider")]
	public class Date : IXmlSerializable
	{
		private DateTime _date;

		public Date() : this(DateTime.MinValue)
		{
		}

		public Date(DateTime date) => _date = date;

		public static XmlQualifiedName MyCustomSchemaProvider(XmlSchemaSet xs)
			=> new XmlQualifiedName("date", "http://www.w3.org/2001/XMLSchema");
		public XmlSchema GetSchema() => null;

		public void ReadXml(XmlReader reader) => _date = reader.ReadElementContentAsDateTime();
		public void WriteXml(XmlWriter writer) => writer.WriteValue(_date.ToString("yyyy-MM-dd"));

		public DateTime GetDate() => DateTime.SpecifyKind(_date, DateTimeKind.Utc);
		public new string ToString() => _date.ToString("yyyy-MM-dd");
	}
}
