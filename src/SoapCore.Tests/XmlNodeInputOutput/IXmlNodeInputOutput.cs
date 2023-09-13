using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;
using System.Xml;

namespace SoapCore.Tests.XmlNodeInputOutput
{
	[ServiceContract]
	public interface IXmlNodeInputOutput
	{
		[OperationContract]
		XmlElement ProcessRequest(string login, string password, XmlElement requestXml);

		[OperationContract]
		XmlElement GetRequest();
	}
}
