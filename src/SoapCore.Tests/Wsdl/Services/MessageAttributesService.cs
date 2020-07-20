using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
	[ServiceContract(Namespace = "http://bagov.net")]
	public interface IMessageHeadersService
	{
		[OperationContract]
		MessageHeadersResponseType GetResponse();
	}

	public class MessageHeadersService : IMessageHeadersService
	{
		public MessageHeadersResponseType GetResponse()
		{
			return new MessageHeadersResponseType();
		}
	}

	[MessageContract]
	public class MessageHeadersResponseType
	{
		[MessageBodyMember(Name = "ModifiedStringProperty")]
		public string StringProperty { get; set; }
	}
}
