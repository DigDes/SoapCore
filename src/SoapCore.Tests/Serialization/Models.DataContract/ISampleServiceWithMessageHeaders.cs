using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[ServiceContract]
	public interface ISampleServiceWithMessageHeaders
	{
		[OperationContract]
		MessageHeadersModelWithBody GetWithBody(MessageHeadersModelWithBody model);

		[OperationContract]
		MessageHeadersModelWithBodyAndNamespace GetWithBodyAndNamespace(MessageHeadersModelWithBodyAndNamespace model);

		[OperationContract]
		MessageHeadersModelWithNamespace GetWithNamespace(MessageHeadersModelWithNamespace model);
	}
}
