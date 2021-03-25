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
	public class TestServiceNotWrapped : IServiceWithMessageContractNotWrapped
	{
		public MessageContractResponseNotWrapped PullData(MessageContractRequestNotWrapped req)
		{
			MessageContractResponseNotWrapped ret = new MessageContractResponseNotWrapped();
			ret.ReferenceNumber = req.ReferenceNumber;
			return ret;
		}
	}
}
