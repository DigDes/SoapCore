using DeepEqual.Syntax;
using Models;
using Moq;
using Shouldly;
using Xunit;

namespace SoapCore.Tests.Serialization
{
	public class SampleServiceSerializationTest : IClassFixture<SampleServiceFixture>
	{
		private readonly SampleServiceFixture fixture;

		public SampleServiceSerializationTest(SampleServiceFixture fixture)
		{
			this.fixture = fixture;
		}

		delegate void PingComplexModelOutAndRefCallback(
			ComplexModelInput inputModel,
			ref ComplexModelResponse responseModelRef,
			ComplexObject data1,
			out ComplexModelResponse responseModelOut,
			ComplexObject data2);

		[Fact]
		public void TestPingComplexModelOutAndRefSerialization()
		{
			this.fixture.sampleServiceMock
				.Setup(x => x.PingComplexModelOutAndRef(
					It.IsAny<ComplexModelInput>(),
					ref It.Ref<ComplexModelResponse>.IsAny,
					It.IsAny<ComplexObject>(),
					out It.Ref<ComplexModelResponse>.IsAny,
					It.IsAny<ComplexObject>()))
				.Callback(new PingComplexModelOutAndRefCallback(
					(ComplexModelInput inputModel_service,
						ref ComplexModelResponse responseModelRef_service,
						ComplexObject data1_service,
						out ComplexModelResponse responseModelOut_service,
						ComplexObject data2_service) =>
					{
						// check input paremeters serialization
						inputModel_service.ShouldDeepEqual(ComplexModelInput.CreateSample1());
						responseModelRef_service.ShouldDeepEqual(ComplexModelResponse.CreateSample1());
						data1_service.ShouldDeepEqual(ComplexObject.CreateSample1());
						data2_service.ShouldDeepEqual(ComplexObject.CreateSample2());
						// sample response
						responseModelRef_service = ComplexModelResponse.CreateSample2();
						responseModelOut_service = ComplexModelResponse.CreateSample3();
					}))
				.Returns(true);

			var responseModelRef_client = ComplexModelResponse.CreateSample1();

			var pingComplexModelOutAndRefResult_client =
				this.fixture.sampleServiceClient.PingComplexModelOutAndRef(
					ComplexModelInput.CreateSample1(),
					ref responseModelRef_client,
					ComplexObject.CreateSample1(),
					out var responseModelOut_client,
					ComplexObject.CreateSample2());

			// check output paremeters serialization
			pingComplexModelOutAndRefResult_client.ShouldBeTrue();
			responseModelRef_client.ShouldDeepEqual(ComplexModelResponse.CreateSample2());
			responseModelOut_client.ShouldDeepEqual(ComplexModelResponse.CreateSample3());
		}
	}
}
