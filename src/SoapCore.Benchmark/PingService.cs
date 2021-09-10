using System.ServiceModel;
using System.Threading.Tasks;

namespace SoapCore.Benchmark
{
	[ServiceContract(Name="PingService", Namespace="http://example.org/PingService")]
	public interface IPingService
	{
		[OperationContract(Action="http://example.org/PingService/Echo", Name="Echo", ReplyAction="http://example.org/PingService/Echo")]
		string Echo(string str);
		[OperationContract(Action="http://example.org/PingService/EchoAsync", Name="EchoAsync", ReplyAction="http://example.org/PingService/EchoAsync")]
		Task<string> EchoAsync(string str);
	}
	public class PingService : IPingService
	{
		public string Echo(string str)
		{
			return $"{str}";
		}

		public Task<string> EchoAsync(string str)
		{
			return Task.FromResult($"{str}");
		}
	}
}
