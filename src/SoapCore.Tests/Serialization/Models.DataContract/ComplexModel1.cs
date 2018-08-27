using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[DataContract]
	public class ComplexModel1
	{
		[DataMember]
		public string StringProperty { get; set; }

		[DataMember]
		public float FloatProperty { get; set; }

		[DataMember]
		public int IntProperty { get; set; }

		[DataMember]
		public List<string> ListProperty { get; set; }

		[DataMember]
		public SampleEnum EnumProperty { get; set; }

		[DataMember]
		public ComplexObject ComplexNestedObjectProperty { get; set; }

		[DataMember]
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
