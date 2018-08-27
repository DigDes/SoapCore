using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
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
