using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public class PaymentResponse
	{
		[DataMember]
		public string ErrorCode { get; set; }

		[DataMember]
		public List<ErrorDetail> ErrorDetailList { get; set; }
	}
}
