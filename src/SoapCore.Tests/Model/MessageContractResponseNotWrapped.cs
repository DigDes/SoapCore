using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Model
{
	[MessageContract(IsWrapped = false)]
	public class MessageContractResponseNotWrapped
	{
		private int _referenceNumber;
		public MessageContractResponseNotWrapped()
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
