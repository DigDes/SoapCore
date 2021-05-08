using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IDataContractWithNonDataMembersService
	{
		[OperationContract]
		TypeWithNonDataMembers Method(TypeWithNonDataMembers argument);
	}

	public class DataContractWithNonDataMembersService : IDataContractWithNonDataMembersService
	{
		public TypeWithNonDataMembers Method(TypeWithNonDataMembers argument)
		{
			return new TypeWithNonDataMembers();
		}
	}
}
