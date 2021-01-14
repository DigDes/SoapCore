using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Model
{
	[MessageContract(IsWrapped = true)]
	public class MessageContractRequest
	{
		private int _referenceNumber;
		public MessageContractRequest()
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
