using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = nameof(ISampleService.PingComplexMessageHeaderArray), WrapperNamespace = ServiceNamespace.Value, IsWrapped = true)]
	public class PingComplexMessageHeaderArrayRequest
	{
		[MessageHeader(Namespace = ServiceNamespace.Value)]
		[System.Xml.Serialization.XmlArrayItemAttribute("ComplexModel1", Namespace = ServiceNamespace.Value + "SomethingDifferent", IsNullable = false)]
		public ComplexModel1[] MyHeaderValue { get; set; }
	}
}
