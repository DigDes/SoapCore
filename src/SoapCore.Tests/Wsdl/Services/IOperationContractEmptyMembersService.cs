using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IOperationContractEmptyMembersService
	{
		[OperationContract]
		string Method(EmptyMembers members);
	}

	public class OperationContractEmptyMembersService : IOperationContractEmptyMembersService
	{
		public string Method(EmptyMembers members)
		{
			return "OK";
		}
	}
}
