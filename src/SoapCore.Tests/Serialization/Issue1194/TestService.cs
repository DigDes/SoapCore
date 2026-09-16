namespace SoapCore.Tests.Serialization.Issue1194;

public sealed class TestService : ITestService
{
	public TestRequestOut Test(TestRequestIn request)
	{
		return new TestRequestOut
		{
			TestHeader = new TestHeader
			{
				From = request.TestHeader.To,
				To = request.TestHeader.From
			},
			TestResult = new TestResponseType
			{
				TestResult = request.TestRequest.TestIn + " result"
			}
		};
	}
}
