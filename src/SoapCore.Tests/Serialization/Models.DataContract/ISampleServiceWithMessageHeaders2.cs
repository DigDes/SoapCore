using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	//same as ISampleServiceWithMessageHeaders, changed return types and parameters for testing.
	[ServiceContract]
	public interface ISampleServiceWithMessageHeaders2
	{
		[OperationContract]
		MessageHeadersModel2 Get2(MessageHeadersModel2 model);

		[OperationContract]
		MessageHeadersModelWithBodyAndNamespace2 GetWithBodyAndNamespace2(MessageHeadersModelWithBodyAndNamespace2 model);

		[OperationContract]
		MessageHeadersModelWithBody2 GetWithBody2(MessageHeadersModelWithBody2 model);
	}
}
