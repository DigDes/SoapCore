using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
	[ServiceContract(Namespace = "http://bagov.net/")]
	public interface IXmlAnnotatedChoiceReturnService
	{
		[OperationContract]
		[return: XmlElement("resultResp", typeof(ResultResponse))]
		[return: XmlElement("integerNumber", typeof(int))]
		object GetResponse(bool boolean);
	}

	public class ResultResponse
	{
		public bool Result { get; set; }
		public string StringResult { get; set; }

		[XmlElement("first", typeof(int))]
		[XmlElement("second", typeof(string))]
		public object[] MultipleResponse { get; set; }
	}

	public class XmlAnnotatedChoiceReturnService : IXmlAnnotatedChoiceReturnService
	{
		public object GetResponse(bool boolean)
		{
			if (boolean)
			{
				return 5;
			}

			return new ResultResponse() { Result = true, StringResult = "test1" };
		}
	}
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
