using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IOperationContractFieldMembersService
	{
		[OperationContract]
		TypeWithFields Method(TypeWithFields argument);
	}

	public class OperationContractFieldMembersService : IOperationContractFieldMembersService
	{
		public TypeWithFields Method(TypeWithFields argument)
		{
			return new TypeWithFields();
		}
	}
}
