using System.Collections.Generic;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services;

[ServiceContract]
public interface IComplexBaseTypeService
{
	[OperationContract]
	DerivedTypeList Method(DerivedTypeList argument);
}

public class ComplexBaseTypeService : IComplexBaseTypeService
{
	public DerivedTypeList Method(DerivedTypeList argument)
	{
		return new DerivedTypeList();
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

public class DerivedTypeList : List<DerivedType>
{
}
