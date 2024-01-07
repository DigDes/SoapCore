using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IEnumWithCustomNamesService
	{
		[OperationContract]
		EnumWithCustomNames? GetEnum(string text);
	}

	public class EnumWithCustomNamesService : IEnumWithCustomNamesService
	{
		public EnumWithCustomNames? GetEnum(string text)
		{
			throw new NotImplementedException();
		}
	}
}
