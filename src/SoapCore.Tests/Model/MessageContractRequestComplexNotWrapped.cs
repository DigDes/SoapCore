using System;
using System.ServiceModel;

namespace SoapCore.Tests.Model
{
	[MessageContract(IsWrapped = false)]
	public class MessageContractRequestComplexNotWrapped
	{
		[MessageBodyMember]
		public ComplexModelInput PostDataBodyMember { get; set; }
	}
}
