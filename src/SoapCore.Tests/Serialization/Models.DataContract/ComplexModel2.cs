using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	// same as ComplexModel1, just for testing
	[DataContract(Namespace = "http://SoapCore.Tests.Serialization.Models.DataContract.ComplexModel2")]
	public class ComplexModel2
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
