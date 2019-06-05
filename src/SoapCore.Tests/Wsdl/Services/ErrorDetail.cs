using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public class ErrorDetail
	{
		[DataMember]
		public string Error { get; set; }

		[DataMember]
		public List<ErrorDetail> SubErrors { get; set; }
	}
}
