using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[DataContract(Namespace = ServiceNamespace.Value)]
	[XmlType(Namespace = ServiceNamespace.Value)]
	public class ComplexModel1
	{
		[DataMember(Order = 0)]
		[XmlAttribute]
		public string StringProperty { get; set; }

		[DataMember(Order = 1)]
		[XmlAttribute]
		public float FloatProperty { get; set; }

		[DataMember(Order = 2)]
		[XmlAttribute]
		public int IntProperty { get; set; }

		[DataMember(Order = 3)]
		[XmlElement(Order = 0)]
		public List<string> ListProperty { get; set; }

		[DataMember(Order = 4)]
		[XmlAttribute]
		public SampleEnum EnumProperty { get; set; }

		[DataMember(Order = 5)]
		[XmlElement(Order = 1)]
		public ComplexObject ComplexNestedObjectProperty { get; set; }

		[DataMember(Order = 6)]
		[XmlElement(Order = 2)]
		public List<ComplexObject> ComplexListProperty { get; set; }

		public static ComplexModel1 CreateSample1()
			=> new ComplexModel1
			{
				StringProperty = $"{nameof(ComplexModel1)} sample one",
				FloatProperty = 1.1F,
				IntProperty = 1,
				ListProperty = new List<string> { "one" },
				EnumProperty = SampleEnum.B,
				ComplexNestedObjectProperty = ComplexObject.CreateSample1(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1()
				}
			};

		public static ComplexModel1 CreateSample2()
			=> new ComplexModel1
			{
				StringProperty = $"{nameof(ComplexModel1)} sample two",
				FloatProperty = 2.2F,
				IntProperty = 2,
				ListProperty = new List<string> { "one", "two" },
				EnumProperty = SampleEnum.C,
				ComplexNestedObjectProperty = ComplexObject.CreateSample2(),
				ComplexListProperty = new List<ComplexObject>
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2()
				}
			};

		public static ComplexModel1 CreateSample3()
			=> new ComplexModel1
			{
				StringProperty = $"{nameof(ComplexModel1)} sample three",
				FloatProperty = 3.3F,
				IntProperty = 3,
				ListProperty = new List<string> { "one", "two", "three" },
				EnumProperty = SampleEnum.D,
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
