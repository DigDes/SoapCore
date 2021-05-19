using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	public class TypeWithUnqualifiedMembers
	{
		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 0)]
		public string StringUnqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Qualified, Order = 1)]
		public string StringQualified { get; set; }

		public int IntQualifiedNoAttribute { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 2)]
		public string[] StringArrayUnqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.None, Order = 3)]
		public string[] StringArrayQualifiedNone { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 4)]
		public UnqType2 UnqType2Unqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Qualified, Order = 5)]
		public UnqType2 UnqType2Qualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 6)]
		public UnqType2[] UnqType2ArrayUnqualified { get; set; }

		public UnqType2[] UnqType2ArrayQualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 7)]
		public List<string> StringListUnqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 8)]
		public DateTime DateTimeUnqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Qualified, Order = 9)]
		public DateTime DateTimeQualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 10)]
		public DateTimeOffset DateTimeOffsetUnqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Qualified, Order = 11)]
		public DateTimeOffset DateTimeOffsetQualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 12)]
		public List<UnqType2> UnqType2ListUnqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 13)]
		public Date DateUnqualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Qualified, Order = 14)]
		public Date DateQualified { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 15)]
		public XElement XElementUnqualified { get; set; }

		[XmlChoiceIdentifier("EnumType")]
		[XmlElement(ElementName = "Word", Type = typeof(string), Form = XmlSchemaForm.Unqualified, Order = 16)]
		[XmlElement(ElementName = "Number", Type = typeof(int), Form = XmlSchemaForm.Unqualified, Order = 16)]
		public object ChoiceUnqualified { get; set; }
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Tightly coupled")]
	public class UnqType2
	{
		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 0)]
		public int IntUnqualified2 { get; set; }

		[XmlElement(Form = XmlSchemaForm.Qualified, Order = 1)]
		public int IntQualified2 { get; set; }

		public string StringQualifiedNoAttribute2 { get; set; }

		[XmlElement(Form = XmlSchemaForm.Unqualified, Order = 2)]
		public string[] StringArrayUnqualified2 { get; set; }

		[XmlElement(Form = XmlSchemaForm.None, Order = 3)]
		public string[] StringArrayQualifiedNone2 { get; set; }
	}
}
