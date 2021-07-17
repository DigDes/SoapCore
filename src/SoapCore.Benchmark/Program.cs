using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http;
using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Logging;

namespace SoapCore.Benchmark
{
	[MemoryDiagnoser]
	[SimpleJob(targetCount: 5)]
	public class EchoBench
	{
		// 0 measures overhead of creating host
		[Params(100)]
		public int LoopNum;
		static readonly string EchoContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <Echo xmlns=""http://example.org/PingService""><str>abc</str></Echo>
  </soap:Body>
</soap:Envelope>
";
		static TestServer CreateTestHost()
		{
			var builder = WebHost.CreateDefaultBuilder()
				.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Critical))
				.UseStartup<Startup>();
			return new TestServer(builder);
		}
		TestServer m_Host;
		[GlobalSetup]
		public void Setup()
		{
			m_Host = CreateTestHost();
		}
		[GlobalCleanup]
		public void Cleanup()
		{
			m_Host.Dispose();
		}
		[Benchmark]
		public async Task EmptyTask()
		{
			for (int i = 0; i < LoopNum; i++)
			{
				using var content = new StringContent(EchoContent, Encoding.UTF8, "text/xml");
				using var res = await m_Host.CreateRequest("/")
					.AddHeader("SOAPAction", "http://example.org/PingService/Echo")
					.And(msg =>
					{
						msg.Content = content;
					}).PostAsync().ConfigureAwait(false);
				res.EnsureSuccessStatusCode();
			}
		}
		[Benchmark]
		public async Task Echo()
		{
			for (int i = 0; i < LoopNum; i++)
			{
				using var content = new StringContent(EchoContent, Encoding.UTF8, "text/xml");
				using var res = await m_Host.CreateRequest("/TestService.asmx")
					.AddHeader("SOAPAction", "http://example.org/PingService/Echo")
					.And(msg =>
					{
						msg.Content = content;
					}).PostAsync().ConfigureAwait(false);
				res.EnsureSuccessStatusCode();
			}
		}
	}
	class Program
	{
		static void Main()
		{
			BenchmarkRunner.Run<EchoBench>();
		}
	}
}
