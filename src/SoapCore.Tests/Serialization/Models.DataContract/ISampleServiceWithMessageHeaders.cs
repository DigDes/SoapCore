using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[ServiceContract]
	public interface ISampleServiceWithMessageHeaders
	{
		[OperationContract]
		MessageHeadersModel Get(MessageHeadersModel model);

		[OperationContract]
		MessageHeadersModelWithBodyAndNamespace GetWithBodyAndNamespace(MessageHeadersModelWithBodyAndNamespace model);

		[OperationContract]
		MessageHeadersModelWithBody GetWithBody(MessageHeadersModelWithBody model);
	}
}
