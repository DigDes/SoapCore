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
	public class TestService : IServiceWithMessageContract
	{
		public MessageContractResponse DoRequest(MessageContractRequest req)
		{
			Model.MessageContractResponse response = new MessageContractResponse();
			response.ReferenceNumber = req.ReferenceNumber;
			return response;
		}

		public MessageContractResponseNotWrapped DoRequest2(MessageContractRequestNotWrapped req)
		{
			Model.MessageContractResponseNotWrapped response = new MessageContractResponseNotWrapped();
			response.ReferenceNumber = req.ReferenceNumber;
			return response;
		}

		public string EmptyRequest(MessageContractRequestEmpty req)
		{
			return "OK";
		}
	}
}
