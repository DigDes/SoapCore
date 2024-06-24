using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	public enum EnumWithCustomNames
	{
		[XmlEnum("F")]
		FirstEnumMember = -2,

		[XmlEnum("S")]
		SecondEnumMember = 1,

		ThirdEnumMember = 0
	}
}
