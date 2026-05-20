using SoapCore.Tests.MessageContract.Models;

namespace SoapCore.Tests.MessageContract
{
	public class Issue1161Service : IServiceIssue1161
	{
		public Issue1161MessageContract Process(Issue1161MessageContract rq) => rq;
	}
}
