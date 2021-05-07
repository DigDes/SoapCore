using System;
using SoapCore.Tests.MessageContract.Models;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.MessageContract
{
	public class TestServiceComplexNotWrapped : IServiceWithMessageContractComplexNotWrapped
	{
		public MessageContractResponseNotWrapped PostData(MessageContractRequestComplexNotWrapped req)
		{
			MessageContractResponseNotWrapped ret = new MessageContractResponseNotWrapped();
			ret.ReferenceNumber = req.PostDataBodyMember.IntProperty;
			return ret;
		}
	}
}
