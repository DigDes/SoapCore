using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	public enum EnumWithCustomNames
	{
		[XmlEnum("F")]
		FirstEnumMember,

		[XmlEnum("S")]
		SecondEnumMember,

		ThirdEnumMember
	}
}
