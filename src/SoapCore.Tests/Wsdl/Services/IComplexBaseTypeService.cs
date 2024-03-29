using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services;

[ServiceContract]
public interface IComplexBaseTypeService
{
	[OperationContract]
	DerivedType Method(DerivedType argument);
}

public class ComplexBaseTypeService : IComplexBaseTypeService
{
	public DerivedType Method(DerivedType argument)
	{
		return new DerivedType();
	}
}

public class BaseType
{
	public string BaseName { get; set; }
}

public class DerivedType : BaseType
{
	public string DerivedName { get; set; }
}
