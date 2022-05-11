using System.Xml;
using System.Xml.Serialization;

namespace SoapCore
{
	[XmlRoot("UsernameToken", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd")]
	public class WsUsernameToken
	{
		[XmlElement("Username")]
		public string Username { get; set; }

		[XmlElement("Password")]
		public PasswordString Password { get; set; }

		[XmlElement("Nonce")]
		public string Nonce { get; set; }

		[XmlElement("Created", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd")]
		public string Created { get; set; }

		public class PasswordString
		{
			[XmlText]
			public string Value { get; set; }

			[XmlAttribute("Type")]
			public string Type { get; set; }
		}
	}
}
