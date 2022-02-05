using System;
using System.Threading.Tasks;

namespace SoapCore.Tests.WsdlFromFile.Services
{
	public class EchoIncludeService : IncludePortType
	{
		public EchoIncludeService()
		{
		}

		public echoIncludeResponse echoInclude(echoIncludeRequest request)
		{
			var response = new echoIncludeResponse();

			try
			{
				//todo
			}
			catch (System.Exception)
			{
				throw;
			}

			return response;
		}

		public Task<echoIncludeResponse> echoIncludeAsync(echoIncludeRequest request)
		{
			throw new NotImplementedException();
		}
	}
}
