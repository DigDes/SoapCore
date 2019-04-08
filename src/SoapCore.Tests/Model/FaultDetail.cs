using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[DataContract]
	public class FaultDetail
	{
		[DataMember]
		public string ExceptionProperty { get; set; }
	}
}
