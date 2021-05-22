using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
	[ServiceContract(Namespace = "http://bagov.net")]
	public interface INullableArrayService
	{
		[OperationContract]
		[XmlSerializerFormat]
		NullableArrayResponse GetResponse(NullableArrayRequest request);
	}

	public class NullableArrayService : INullableArrayService
	{
		public NullableArrayResponse GetResponse(NullableArrayRequest request)
		{
			return new NullableArrayResponse();
		}
	}

	[MessageContract]
	public class NullableArrayResponse
	{
		public long?[] Array { get; set; }
	}

	[MessageContract]
	public class NullableArrayRequest
	{
		public long?[] Array { get; set; }
	}
}
