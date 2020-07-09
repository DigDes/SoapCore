using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[KnownType(typeof(ComplexInheritanceModelInputA))]
	[KnownType(typeof(ComplexInheritanceModelInputB))]
	[DataContract(Name = "ComplexInheritanceModelInputBase")]
	public abstract class ComplexInheritanceModelInputBase
	{
		[DataMember]
		public string StringProperty { get; set; }

		[DataMember]
		public abstract string Example { get; set; }
	}
}
