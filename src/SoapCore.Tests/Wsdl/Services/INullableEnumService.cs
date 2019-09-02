using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface INullableEnumService
	{
		[OperationContract]
		NulEnum? GetEnum(string text);
	}

	public class NullableEnumService : INullableEnumService
	{
		public NulEnum? GetEnum(string text)
		{
			throw new NotImplementedException();
		}
	}
}
