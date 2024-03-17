using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services;

[ServiceContract]
public interface ISpecifiedBoolService
{
	[OperationContract]
	TypeWithSpecifiedEnum Method(TypeWithSpecifiedEnum argument);
}

public class SpecifiedBoolService : ISpecifiedBoolService
{
	public TypeWithSpecifiedEnum Method(TypeWithSpecifiedEnum argument)
	{
		return new TypeWithSpecifiedEnum();
	}
}

public class TypeWithSpecifiedEnum
{
	[XmlIgnore]
	public bool EnumSpecified { get; set; }
	public NulEnum Enum { get; set; }
	public NulEnum NormalEnum { get; set; }
}
