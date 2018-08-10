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
			ref ComplexModelResponse responseModelRef1,
			ComplexObject data1,
			ref ComplexModelResponse responseModelRef2,
			ComplexObject data2,
			out ComplexModelResponse responseModelOut1,
			out ComplexModelResponse responseModelOut2);

		[Fact]
		public void TestPingComplexModelOutAndRefSerialization()
		{
			this.fixture.sampleServiceMock
				.Setup(x => x.PingComplexModelOutAndRef(
					It.IsAny<ComplexModelInput>(),
					ref It.Ref<ComplexModelResponse>.IsAny,
					It.IsAny<ComplexObject>(),
					ref It.Ref<ComplexModelResponse>.IsAny,
					It.IsAny<ComplexObject>(),
					out It.Ref<ComplexModelResponse>.IsAny,
					out It.Ref<ComplexModelResponse>.IsAny))
				.Callback(new PingComplexModelOutAndRefCallback(
					(ComplexModelInput inputModel_service,
						ref ComplexModelResponse responseModelRef1_service,
						ComplexObject data1_service,
						ref ComplexModelResponse responseModelRef2_service,
						ComplexObject data2_service,
						out ComplexModelResponse responseModelOut1_service,
						out ComplexModelResponse responseModelOut2_service) =>
					{
						// check input paremeters serialization
						inputModel_service.ShouldDeepEqual(ComplexModelInput.CreateSample1());
						responseModelRef1_service.ShouldDeepEqual(ComplexModelResponse.CreateSample1());
						responseModelRef2_service.ShouldDeepEqual(ComplexModelResponse.CreateSample2());
						data1_service.ShouldDeepEqual(ComplexObject.CreateSample1());
						data2_service.ShouldDeepEqual(ComplexObject.CreateSample2());
						// sample response
						responseModelRef1_service = ComplexModelResponse.CreateSample2();
						responseModelRef2_service = ComplexModelResponse.CreateSample1();
						responseModelOut1_service = ComplexModelResponse.CreateSample3();
						responseModelOut2_service = ComplexModelResponse.CreateSample1();
					}))
				.Returns(true);

			var responseModelRef1_client = ComplexModelResponse.CreateSample1();
			var responseModelRef2_client = ComplexModelResponse.CreateSample2();

			var pingComplexModelOutAndRefResult_client =
				this.fixture.sampleServiceClient.PingComplexModelOutAndRef(
					ComplexModelInput.CreateSample1(),
					ref responseModelRef1_client,
					ComplexObject.CreateSample1(),
					ref responseModelRef2_client,
					ComplexObject.CreateSample2(),
					out var responseModelOut1_client,
					out var responseModelOut2_client);

			// check output paremeters serialization
			pingComplexModelOutAndRefResult_client.ShouldBeTrue();
			responseModelRef1_client.ShouldDeepEqual(ComplexModelResponse.CreateSample2());
			responseModelRef2_client.ShouldDeepEqual(ComplexModelResponse.CreateSample1());
			responseModelOut1_client.ShouldDeepEqual(ComplexModelResponse.CreateSample3());
			responseModelOut2_client.ShouldDeepEqual(ComplexModelResponse.CreateSample1());
		}
	}
}
