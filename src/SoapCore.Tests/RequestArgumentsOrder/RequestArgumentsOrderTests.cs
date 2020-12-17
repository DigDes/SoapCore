using Moq;
using Shouldly;
using Xunit;

namespace SoapCore.Tests.RequestArgumentsOrder
{
	public class RequestArgumentsOrderTests : IClassFixture<ServiceFixture<IOriginalParametersOrderService, IReversedParametersOrderService>>
	{
		private readonly ServiceFixture<IOriginalParametersOrderService, IReversedParametersOrderService> _fixture;

		public RequestArgumentsOrderTests(ServiceFixture<IOriginalParametersOrderService, IReversedParametersOrderService> fixture)
		{
			_fixture = fixture;
		}

		[Theory]
		[MemberData(nameof(ServiceFixture<IOriginalParametersOrderService, IReversedParametersOrderService>.SoapSerializersList), MemberType = typeof(ServiceFixture<IOriginalParametersOrderService, IReversedParametersOrderService>))]
		public void TestOriginalParametersOrder(SoapSerializer soapSerializer)
		{
			var client = _fixture.GetOriginalRequestArgumentsOrderClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.TwoStringParameters(It.IsAny<string>(), It.IsAny<string>()))
				.Callback(
					(string first, string second) =>
					{
						first.ShouldBe("1");
						second.ShouldBe("2");
					});

			client.TwoStringParameters(first: "1", second: "2");
		}

		[Theory]
		[MemberData(nameof(ServiceFixture<IOriginalParametersOrderService, IReversedParametersOrderService>.SoapSerializersList), MemberType = typeof(ServiceFixture<IOriginalParametersOrderService, IReversedParametersOrderService>))]
		public void TestReversedParametersOrder(SoapSerializer soapSerializer)
		{
			var client = _fixture.GetReversedRequestArgumentsOrderClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.TwoStringParameters(It.IsAny<string>(), It.IsAny<string>()))
				.Callback(
					(string first, string second) =>
					{
						first.ShouldBe("1");
						second.ShouldBe("2");
					});

			client.TwoStringParameters(second: "2", first: "1");
		}
	}
}
