using System.Runtime.Serialization;

namespace SoapCore.Tests.OperationDescription.Model
{
	//TODO: Fix the meta generation of fault namespaces, to remove this limitation
	[DataContract(Namespace = "Currently, this MUST be set to the same as your ServiceContract namespace for catching to work on the client.")]
	public class TypedSoapFault
	{
		[DataMember(Name = "This name is currently ignored")]
		public string MyIncludedProperty { get; set; }

		[IgnoreDataMember]
		[DataMember]
		public string MyExcludedProperty { get; set; }
	}
}
