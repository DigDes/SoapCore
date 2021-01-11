using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.MessageContract
{
	public class TestService : IServiceWithMessageContract
	{
		public string EmptyRequest(MessageContractRequestEmpty req)
		{
			return "OK";
		}
	}
}
