using System.ServiceModel;

namespace ServerWithUi.HeaderDemo
{
	/// <summary>
	/// A tiny contract exposed at /NoWsdl.svc with GET disabled. It shows two explorer features: the generic-envelope
	/// fallback when the WSDL cannot be fetched, and the custom request headers editor.
	/// </summary>
	[ServiceContract(Namespace = "http://soapcore.dev/samples/headerdemo")]
	public interface IHeaderDemoService
	{
		[OperationContract]
		string Echo(string text);

		// Needs the X-Demo-Token request header.
		[OperationContract]
		string EchoWithToken(string text);
	}
}
