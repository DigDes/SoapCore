using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[KnownType(typeof(ComplexInheritanceModelInputB))]
	[DataContract(Name = "ComplexInheritanceModelInputA")]
	public class ComplexInheritanceModelInputA : ComplexInheritanceModelInputBase
	{
		[DataMember]
		public override string Example { get; set; }
	}
}
