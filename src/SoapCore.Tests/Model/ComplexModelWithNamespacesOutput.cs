using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[DataContract(Namespace = "DataModel")]
	public class ComplexModelWithNamespacesOutput
	{
        [DataMember]
        public string Output { get; set; }
	}
}
