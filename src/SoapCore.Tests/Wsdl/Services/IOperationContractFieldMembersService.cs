using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IOperationContractFieldMembersService
	{
		[OperationContract]
		FieldMembers Method(FieldMembers members);
	}

	public class OperationContractFieldMembersService : IOperationContractFieldMembersService
	{
		public FieldMembers Method(FieldMembers members)
		{
			return new FieldMembers();
		}
	}
}
