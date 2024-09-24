using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using SoapCore.ServiceModel;
#pragma warning disable SA1401 // Fields should be private

namespace SoapCore.Tests.Wsdl.Services;
[ServiceContract]
public interface IServiceWithSoapHeaders
{
	[OperationContract]
	[AuthenticationContextSoapHeader]
	public void Method();
}

public class ServiceWithSoapHeaders : IServiceWithSoapHeaders
{
	public void Method()
	{
	}
}

public sealed class AuthenticationContextSoapHeader : SoapHeaderAttribute
{
	[XmlAnyAttribute]
	public XmlAttribute[] XAttributes;

	public string OperatorCode { get; set; }
	public string Password { get; set; }
}
