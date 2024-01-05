using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IComplexComplexTypeWithCustomXmlNamesService
	{
		[OperationContract]
		ComplexComplexType Method(out string message);
	}

	public class ComplexComplexTypeWithCustomXmlNamesService : IComplexComplexTypeWithCustomXmlNamesService
	{
		public ComplexComplexType Method(out string message)
		{
			throw new NotImplementedException();
		}
	}
}
