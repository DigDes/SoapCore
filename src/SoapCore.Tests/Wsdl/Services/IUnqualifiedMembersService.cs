using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IUnqualifiedMembersService
	{
		[OperationContract(Action = "http://sampleservice.net/webservices/Method", ReplyAction = "*")]
		[XmlSerializerFormat(SupportFaults = true)]
		TypeWithUnqualifiedMembers Method(TypeWithUnqualifiedMembers request);
	}

	public class UnqualifiedMembersService : IUnqualifiedMembersService
	{
		public TypeWithUnqualifiedMembers Method(TypeWithUnqualifiedMembers request)
		{
			throw new NotImplementedException();
		}
	}
}
