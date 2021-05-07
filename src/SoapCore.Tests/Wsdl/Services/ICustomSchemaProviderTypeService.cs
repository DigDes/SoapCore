using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface ICustomSchemaProviderTypeService
	{
		[OperationContract]
		Date Method(Date argument);
	}

	public class CustomSchemaProviderTypeService : ICustomSchemaProviderTypeService
	{
		public Date Method(Date argument)
		{
			return new Date();
		}
	}
}
