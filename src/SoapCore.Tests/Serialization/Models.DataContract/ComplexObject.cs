using System.Runtime.Serialization;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[DataContract]
	public class ComplexObject
	{
		[DataMember]
		public string StringProperty { get; set; }

		[DataMember]
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
