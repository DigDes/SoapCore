using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[DataContract(Namespace = "DataContractNestedNamespace")]
	public class ComplexModelNestedInput
	{
		[DataMember(Name = "TestNestedProperty")]
		public string NestedStringProperty { get; set; }
	}
}
