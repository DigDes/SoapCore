using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SoapCore.Tests.MessageContract.Models;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.MessageContract
{
	public class TestServiceWrapped : IServiceWithMessageContractWrapped
	{
		public MessageContractResponse PullData(MessageContractRequest req)
		{
			MessageContractResponse ret = new MessageContractResponse();
			ret.ReferenceNumber = req.ReferenceNumber;
			return ret;
		}
	}
}
