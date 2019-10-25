using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public class CircularReferenceFirstObject
	{
		[DataMember]
		public CircularReferenceSecondObject SecondObject { get; set; }
	}
}
