using System.Runtime.Serialization;

namespace SoapCore
{
	[DataContract(Name = "UsernameToken", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd")]
	public class WsUsernameToken
	{
		[DataMember(Name = "Username", Order = 1, IsRequired = true)]
		public string Username { get; set; }
		[DataMember(Name = "Password", Order = 2, IsRequired = true)]
		public string Password { get; set; }
	}
}
