using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services;

[ServiceContract]
public interface IEnumService
{
	[OperationContract]
	TypeWithEnums Method(TypeWithEnums argument);
}

public class EnumService : IEnumService
{
	public TypeWithEnums Method(TypeWithEnums argument)
	{
		return new TypeWithEnums();
	}
}

public class TypeWithEnums
{
	public NulEnum Enum { get; set; }
	public NulEnum? NullEnum { get; set; }
}
