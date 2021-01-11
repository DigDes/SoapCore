using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Model
{
	[MessageContract(IsWrapped = false)]
	public class MessageContractRequestEmpty
	{
		public MessageContractRequestEmpty()
		{
		}
	}
}
