using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IShouldSerializeTypeMemberService
	{
		[OperationContract]
		TypeWithShouldSerializeMember Method();
	}

	public class ShouldSerializeTypeMemberService : IShouldSerializeTypeMemberService
	{
		public TypeWithShouldSerializeMember Method()
		{
			throw new NotImplementedException();
		}
	}
}
