using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface INonNullableEnumService
	{
		[OperationContract]
		NulEnum GetEnum(string text);
	}

	public class NonNullableEnumService : INonNullableEnumService
	{
		public NulEnum GetEnum(string text)
		{
			throw new NotImplementedException();
		}
	}
}
