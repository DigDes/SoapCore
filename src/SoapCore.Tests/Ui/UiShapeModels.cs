using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace SoapCore.Tests.Ui
{
	// Contract types that cover what the explorer's WSDL parser has to understand:
	// complex types, an enum, a list, Guid, DateTime, a nullable and inheritance.
	// Members use explicit Order so DataContractSerializer and XmlSerializer agree on the element order.
	// The data namespace starts with "http" on purpose: SoapCore prefixes any other DataContract namespace (for example "urn:...")
	// with http://schemas.datacontract.org/2004/07/ in the schema it writes, but not in the references to the types, so such a WSDL is inconsistent.
	public static class UiShapeModels
	{
		public const string DataNamespace = "http://soapcore.dev/tests/ui-shape";

		[DataContract(Namespace = DataNamespace)]
		public enum Color
		{
			[EnumMember]
			Red,

			[EnumMember]
			Green,
		}

		[DataContract(Namespace = DataNamespace)]
		public class Request
		{
			[DataMember(Order = 0)]
			public Guid Id { get; set; }

			[DataMember(Order = 1)]
			public DateTime When { get; set; }

			[DataMember(Order = 2)]
			public Color Color { get; set; }

			[DataMember(Order = 3)]
			public int? Count { get; set; }

			[DataMember(Order = 4)]
			public string Name { get; set; }
		}

		[DataContract(Namespace = DataNamespace)]
		public class Item
		{
			[DataMember(Order = 0)]
			public string Label { get; set; }

			[DataMember(Order = 1)]
			public decimal Amount { get; set; }
		}

		[DataContract(Namespace = DataNamespace)]
		[KnownType(typeof(Derived))]
		[XmlInclude(typeof(Derived))]
		public class Base
		{
			[DataMember(Order = 0)]
			public string Name { get; set; }
		}

		[DataContract(Namespace = DataNamespace)]
		public class Derived : Base
		{
			[DataMember(Order = 1)]
			public int Extra { get; set; }
		}

		[DataContract(Namespace = DataNamespace)]
		public class Reply
		{
			[DataMember(Order = 0)]
			public List<Item> Items { get; set; }

			[DataMember(Order = 1)]
			public Base Shape { get; set; }
		}
	}
}
