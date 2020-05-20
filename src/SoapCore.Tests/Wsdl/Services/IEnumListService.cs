using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IEnumListService
	{
		[OperationContract]
		List<EnumListService.TestEnum> List();
	}

	public class EnumListService : IEnumListService
	{
		[DataContract]
		public enum TestEnum
		{
			[EnumMember]
			A,

			[EnumMember]
			B
		}

		public List<TestEnum> List() => throw new NotImplementedException();
	}
}
