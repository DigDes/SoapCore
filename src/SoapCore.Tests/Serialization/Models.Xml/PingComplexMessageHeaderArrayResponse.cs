using System.ServiceModel;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = nameof(PingComplexMessageHeaderArrayResponse), WrapperNamespace = ServiceNamespace.Value, IsWrapped = true)]
	public class PingComplexMessageHeaderArrayResponse
	{
	}
}
