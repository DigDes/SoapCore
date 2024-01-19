using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public class FailedOperation
	{
		[DataMember]
		public string Message { get; set; }
	}
}
