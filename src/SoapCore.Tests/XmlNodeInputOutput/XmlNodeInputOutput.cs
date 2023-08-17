using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace SoapCore.Tests.XmlNodeInputOutput
{
	public class XmlNodeInputOutput : IXmlNodeInputOutput
	{
		public XmlNodeInputOutput()
		{
		}

		public XmlElement ProcessRequest(string login, string password, XmlElement requestXml)
		{
			if (password == "Password")
			{
				return requestXml;
			}
			else
			{
				XmlDocument xdError = new XmlDocument();
				xdError.LoadXml("<Error><Message>Incorrect Password</Message></Error>");
				return xdError.DocumentElement;
			}
		}

		public XmlElement GetRequest()
		{
			XmlDocument xdResponse = new XmlDocument();
			xdResponse.LoadXml("<Response><Message>A response</Message></Response>");
			return xdResponse.DocumentElement;
		}
	}
}
