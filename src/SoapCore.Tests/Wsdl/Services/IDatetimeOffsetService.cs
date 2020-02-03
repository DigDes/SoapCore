using System;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IDatetimeOffsetService
	{
		[OperationContract]
		DateTimeOffset Method(DateTimeOffset model);
	}

	public class DateTimeOffsetService : IDatetimeOffsetService
	{
		public DateTimeOffset Method(DateTimeOffset model)
		{
			throw new NotImplementedException();
		}
	}
}
