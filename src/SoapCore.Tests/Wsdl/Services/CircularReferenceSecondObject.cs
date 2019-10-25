using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public class CircularReferenceSecondObject
	{
		[DataMember]
		public CircularReferenceFirstObject FirstObject { get; set; }
	}
}
