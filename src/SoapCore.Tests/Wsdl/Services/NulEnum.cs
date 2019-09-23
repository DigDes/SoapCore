using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public enum NulEnum
	{
		[EnumMember]
		A,

		[EnumMember]
		B
	}
}
