using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[DataContract(Name = "ComplexInheritanceModelInputB")]
	public class ComplexInheritanceModelInputB : ComplexInheritanceModelInputA
	{
		[DataMember]
		public int Example2 { get; set; }
	}
}
