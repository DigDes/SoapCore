using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[DataContract(Namespace = ServiceNamespace.Value)]
	[XmlType(Namespace = ServiceNamespace.Value)]
	public enum SampleEnum
	{
		[EnumMember]
		A,

		[EnumMember]
		B,

		[EnumMember]
		C,

		[EnumMember]
		D
	}
}
