using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public class OperationFault
	{
		[DataMember]
		public string Code { get; set; }

		[DataMember]
		public string Message { get; set; }
	}
}
