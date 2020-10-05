using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = "MyWrapperNameIsDifferentFromTheClass", WrapperNamespace = ServiceNamespace.Value, IsWrapped = true)]
	public class PingComplexMessageMessageContractAttributeResponse
	{
		[MessageBodyMember(Namespace = ServiceNamespace.Value, Order = 0)]
		public ComplexModel1 ComplexProperty { get; set; }
	}
}
