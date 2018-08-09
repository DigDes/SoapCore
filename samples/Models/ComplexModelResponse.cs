using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace Models
{
	[DataContract(Namespace = ServiceNamespace.Value)]
	[XmlType(Namespace = ServiceNamespace.Value)]
	public class ComplexModelResponse
	{
		[DataMember(Order = 0)]
		[XmlAttribute]
		public float FloatProperty { get; set; }

		[DataMember(Order = 1)]
		[XmlAttribute]
		public string StringProperty { get; set; }

		[DataMember(Order = 2)]
		[XmlAttribute]
		public List<string> ListProperty { get; set; }

		[DataMember(Order = 3)]
		[XmlAttribute]
		public SampleEnum EnumProperty { get; set; }

		//[DataMember(Order = ...)]
		//[XmlAttribute]
		//public DateTimeOffset DateTimeOffsetProperty { get; set; }

		[DataMember(Order = 4)]
		[XmlElement(Order = 0)]
		public ComplexObject ComplexNestedObjectProperty { get; set; }

		[DataMember(Order = 5)]
		[XmlElement(Order = 1)]
		public List<ComplexObject> ComplexListProperty { get; set; }

		public static ComplexModelResponse CreateSample1()
			=> new ComplexModelResponse
			{
				FloatProperty = 1.1F,
				StringProperty = $"{nameof(ComplexModelResponse)} sample one",
				ListProperty = new List<string> { "one" },
				EnumProperty = SampleEnum.A,
				// DateTimeOffsetProperty = DateTimeOffset.MinValue,
				ComplexNestedObjectProperty = ComplexObject.CreateSample1(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1()
				}
			};

		public static ComplexModelResponse CreateSample2()
			=> new ComplexModelResponse
			{
				FloatProperty = 2.2F,
				StringProperty = $"{nameof(ComplexModelResponse)} sample two",
				ListProperty = new List<string> { "one" },
				EnumProperty = SampleEnum.B,
				// DateTimeOffsetProperty = DateTimeOffset.MaxValue,
				ComplexNestedObjectProperty = ComplexObject.CreateSample2(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2()
				}
			};

		public static ComplexModelResponse CreateSample3()
			=> new ComplexModelResponse
			{
				FloatProperty = 3.3F,
				StringProperty = $"{nameof(ComplexModelResponse)} sample three",
				ListProperty = new List<string> { "one" },
				EnumProperty = SampleEnum.C,
				//DateTimeOffsetProperty = DateTimeOffset.MaxValue,
				ComplexNestedObjectProperty = ComplexObject.CreateSample3(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2(),
					ComplexObject.CreateSample2()
				}
			};
	}
}
