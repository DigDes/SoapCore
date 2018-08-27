using System.Runtime.Serialization;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[DataContract]
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
