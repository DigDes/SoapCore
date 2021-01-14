using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Model
{
	[MessageContract(IsWrapped = true)]
	public class MessageContractResponse
	{
		private int _referenceNumber;
		public MessageContractResponse()
		{
		}

		[MessageBodyMember]
		public int ReferenceNumber
		{
			get
			{
				return _referenceNumber;
			}
			set
			{
				_referenceNumber = value;
			}
		}
	}
}
