using System;
using System.Threading.Tasks;

namespace SoapCore.Tests
{
	public class TestService : ITestService
	{
		public string Ping(string s)
		{
			return s;
		}

		public string EmptyArgs()
		{
			return "EmptyArgs";
		}

		public string SingleInteger(int i)
		{
			return i.ToString();
		}

		public Task<string> AsyncMethod()
		{
			return Task.FromResult<string>("hello, async");
		}

		public bool IsNull(double? d)
		{
			return !d.HasValue;
		}
	}
}
