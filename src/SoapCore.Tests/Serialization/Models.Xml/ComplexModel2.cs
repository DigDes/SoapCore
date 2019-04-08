using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	// same as ComplexModel1, but different serialization attributes and samples
	[DataContract(Namespace = ServiceNamespace.Value)]
	[XmlType(Namespace = ServiceNamespace.Value)]
	public class ComplexModel2
	{
		[DataMember(Order = 0)]
		[XmlElement(Order = 0)]
		public string StringProperty { get; set; }

		[DataMember(Order = 1)]
		[XmlElement(Order = 1)]
		public float FloatProperty { get; set; }

		[DataMember(Order = 2)]
		[XmlElement(Order = 2)]
		public int IntProperty { get; set; }

		[DataMember(Order = 3)]
		[XmlArray(Order = 3)]
		public List<string> ListProperty { get; set; }

		[DataMember(Order = 4)]
		[XmlElement(Order = 4)]
		public SampleEnum EnumProperty { get; set; }

		[DataMember(Order = 5)]
		[XmlElement(Order = 5)]
		public ComplexObject ComplexNestedObjectProperty { get; set; }

		[DataMember(Order = 6)]
		[XmlArray(Order = 6)]
		public List<ComplexObject> ComplexListProperty { get; set; }

		public static ComplexModel2 CreateSample1()
			=> new ComplexModel2
			{
				StringProperty = $"{nameof(ComplexModel2)} sample one",
				FloatProperty = 1.11F,
				IntProperty = 11,
				ListProperty = new List<string> { "one2" },
				EnumProperty = SampleEnum.D,
				ComplexNestedObjectProperty = ComplexObject.CreateSample1(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1()
				}
			};

		public static ComplexModel2 CreateSample2()
			=> new ComplexModel2
			{
				StringProperty = $"{nameof(ComplexModel2)} sample two",
				FloatProperty = 2.22F,
				IntProperty = 22,
				ListProperty = new List<string> { "one2", "two2" },
				EnumProperty = SampleEnum.B,
				ComplexNestedObjectProperty = ComplexObject.CreateSample2(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2()
				}
			};

		public static ComplexModel2 CreateSample3()
			=> new ComplexModel2
			{
				StringProperty = $"{nameof(ComplexModel2)} sample three",
				FloatProperty = 3.33F,
				IntProperty = 33,
				ListProperty = new List<string> { "one2", "two2", "three2" },
				EnumProperty = SampleEnum.C,
				ComplexNestedObjectProperty = ComplexObject.CreateSample3(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2(),
					ComplexObject.CreateSample3()
				}
			};
	}
}
