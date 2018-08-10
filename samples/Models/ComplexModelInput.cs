using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Models
{
	[DataContract(Namespace = ServiceNamespace.Value)]
	[XmlType(Namespace = ServiceNamespace.Value)]
	public enum SampleEnum
	{
		[EnumMember]
		A,

		[EnumMember]
		B,

		[EnumMember]
		C
	}

	[DataContract(Namespace = ServiceNamespace.Value)]
	[XmlType(Namespace = ServiceNamespace.Value)]
	public class ComplexModelInput
	{
		[DataMember(Order = 0)]
		[XmlAttribute]
		public string StringProperty { get; set; }

		[DataMember(Order = 1)]
		[XmlAttribute]
		public int IntProperty { get; set; }

		[DataMember(Order = 2)]
		[XmlElement(Order = 0)]
		public List<string> ListProperty { get; set; }

		[DataMember(Order = 3)]
		[XmlAttribute]
		public SampleEnum EnumProperty { get; set; }

		//[DataMember(Order = ...)]
		//[XmlAttribute]
		//public DateTimeOffset DateTimeOffsetProperty { get; set; }

		[DataMember(Order = 4)]
		[XmlElement(Order = 1)]
		public ComplexObject ComplexNestedObjectProperty { get; set; }

		[DataMember(Order = 5)]
		[XmlElement(Order = 2)]
		public List<ComplexObject> ComplexListProperty { get; set; }

		public static ComplexModelInput CreateSample1()
			=> new ComplexModelInput
			{
				StringProperty = $"{nameof(ComplexModelInput)} sample one",
				IntProperty = 1,
				ListProperty = new List<string> { "one" },
				EnumProperty = SampleEnum.A,
				// DateTimeOffsetProperty = DateTimeOffset.MinValue,
				ComplexNestedObjectProperty = ComplexObject.CreateSample1(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1()
				}
			};

		public static ComplexModelInput CreateSample2()
			=> new ComplexModelInput
			{
				StringProperty = $"{nameof(ComplexModelInput)} sample two",
				IntProperty = 2,
				ListProperty = new List<string> { "two" },
				EnumProperty = SampleEnum.B,
				// DateTimeOffsetProperty = DateTimeOffset.MaxValue,
				ComplexNestedObjectProperty = ComplexObject.CreateSample2(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2()
				}
			};
	}

	[DataContract(Namespace = ServiceNamespace.Value)]
	[XmlType(Namespace = ServiceNamespace.Value)]
	public class ComplexObject
	{
		[DataMember(Order = 0)]
		[XmlAttribute]
		public string StringProperty { get; set; }

		[DataMember(Order = 1)]
		[XmlAttribute]
		public int IntProperty { get; set; }

		public static ComplexObject CreateSample1()
			=> new ComplexObject
			{
				IntProperty = 1,
				StringProperty = $"{nameof(ComplexObject)} sample one"
			};

		public static ComplexObject CreateSample2()
			=> new ComplexObject
			{
				IntProperty = 2,
				StringProperty = $"{nameof(ComplexObject)} sample two"
			};

		public static ComplexObject CreateSample3()
			=> new ComplexObject
			{
				IntProperty = 3,
				StringProperty = $"{nameof(ComplexObject)} sample three"
			};
	}
}
