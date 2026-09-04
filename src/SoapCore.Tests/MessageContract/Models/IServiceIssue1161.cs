using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.MessageContract.Models
{
	[ServiceContract(Namespace = "http://tempuri.org/")]
	public interface IServiceIssue1161
	{
		[OperationContract]
		[XmlSerializerFormat(SupportFaults = true)]
		Issue1161MessageContract Process(Issue1161MessageContract rq);
	}
}
