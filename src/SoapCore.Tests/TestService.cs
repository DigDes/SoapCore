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

		public void ThrowException()
		{
			throw new Exception();
		}

		public void ThrowExceptionWithMessage(string message)
		{
			throw new Exception(message);
		}

		public string Overload(double d)
		{
			return "Overload(double)";
		}

		public string Overload(string s)
		{
			return "Overload(string)";
		}

		public bool OperationName() => true;

		public void OutParam(out string message)
		{
			message = "hello, world";
		}

		public void RefParam(ref string message)
		{
			message = "hello, world";
		}
	}
}
