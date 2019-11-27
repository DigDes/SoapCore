using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DeepEqual.Syntax;
using Moq;
using Shouldly;
using SoapCore.Tests.Serialization.Models.Xml;
using Xunit;

namespace SoapCore.Tests.Serialization
{
	[Collection("serialization")]
	public class WcfXmlSerializationTests : IClassFixture<ServiceFixture<ISampleService>>
	{
		private readonly ServiceFixture<ISampleService> _fixture;

		public WcfXmlSerializationTests(ServiceFixture<ISampleService> fixture)
		{
			_fixture = fixture;
		}

		private delegate void PingComplexModelOutAndRefCallback(
			ComplexModel1 inputModel,
			ref ComplexModel2 responseModelRef1,
			ComplexObject data1,
			ref ComplexModel1 responseModelRef2,
			ComplexObject data2,
			out ComplexModel2 responseModelOut1,
			out ComplexModel1 responseModelOut2);

		private delegate void EnumMethodCallback(out SampleEnum e);

		private delegate void VoidMethodCallback(out string s);

		[Theory]
		[MemberData(nameof(ServiceFixture<ISampleService>.SoapSerializersList), MemberType = typeof(ServiceFixture<ISampleService>))]
		public void TestPingSerialization(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			const string input_value = "input_value";
			const string output_value = "output_value";

			_fixture.ServiceMock
				.Setup(x => x.Ping(It.IsAny<string>()))
				.Callback(
					(string s_service) =>
					{
						// check input paremeters serialization
						s_service.ShouldBe(input_value);
					})
				.Returns(output_value);

			var pingResult_client = sampleServiceClient.Ping(input_value);

			// check output paremeters serialization
			pingResult_client.ShouldBe(output_value);
		}

		[Theory]
		[MemberData(nameof(ServiceFixture<ISampleService>.SoapSerializersList), MemberType = typeof(ServiceFixture<ISampleService>))]
		public void TestEnumMethodSerialization(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			const SampleEnum output_value = SampleEnum.C;

			_fixture.ServiceMock
				.Setup(x => x.EnumMethod(out It.Ref<SampleEnum>.IsAny))
				.Callback(new WcfXmlSerializationTests.EnumMethodCallback(
					(out SampleEnum e_service) =>
					{
						// sample response
						e_service = output_value;
					}))
				.Returns(true);

			var enumMethodResult_client = sampleServiceClient.EnumMethod(out var e_client);

			// check output paremeters serialization
			enumMethodResult_client.ShouldBe(true);
			e_client.ShouldBe(output_value);
		}

		[Theory]
		[MemberData(nameof(ServiceFixture<ISampleService>.SoapSerializersList), MemberType = typeof(ServiceFixture<ISampleService>))]
		public void TestVoidMethodSerialization(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			const string output_value = "output_value";

			_fixture.ServiceMock
				.Setup(x => x.VoidMethod(out It.Ref<string>.IsAny))
				.Callback(new WcfXmlSerializationTests.VoidMethodCallback(
					(out string s_service) =>
					{
						// sample response
						s_service = output_value;
					}));

			sampleServiceClient.VoidMethod(out var s_client);

			// check output paremeters serialization
			s_client.ShouldBe(output_value);
		}

		[Theory]
		[MemberData(nameof(ServiceFixture<ISampleService>.SoapSerializersList), MemberType = typeof(ServiceFixture<ISampleService>))]
		public async Task TestAsyncMethodSerialization(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			const int output_value = 123;

			_fixture.ServiceMock
				.Setup(x => x.AsyncMethod())
				.Returns(() => Task.Run(() => output_value));

			var asyncMethodResult_client = await sampleServiceClient.AsyncMethod();

			// check output paremeters serialization
			asyncMethodResult_client.ShouldBe(output_value);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer, null, null)]
		[InlineData(SoapSerializer.XmlWcfSerializer, true, 1)]
		[InlineData(SoapSerializer.XmlWcfSerializer, false, 2)]
		[InlineData(SoapSerializer.XmlSerializer, null, null)]
		[InlineData(SoapSerializer.XmlSerializer, true, 1)]
		[InlineData(SoapSerializer.XmlSerializer, false, 2)]
		[InlineData(SoapSerializer.DataContractSerializer, null, null)]
		[InlineData(SoapSerializer.DataContractSerializer, true, 1)]
		[InlineData(SoapSerializer.DataContractSerializer, false, 2)]
		public void TestNullableMethodSerialization(SoapSerializer soapSerializer, bool? input_value, int? output_value)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.NullableMethod(It.IsAny<bool?>()))
				.Callback(
					(bool? arg_service) =>
					{
						// check input paremeters serialization
						arg_service.ShouldBe(input_value);
					})
				.Returns(output_value);

			var nullableMethodResult_client = sampleServiceClient.NullableMethod(input_value);

			// check output paremeters serialization
			nullableMethodResult_client.ShouldBe(output_value);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestPingComplexArrayModel(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.PingComplexModelArray(It.IsAny<ComplexModel1[]>(), It.IsAny<ComplexModel2[]>()))
				.Callback((ComplexModel1[] input, ComplexModel2[] input2) =>
				{
					input.ShouldDeepEqual(new[] { ComplexModel1.CreateSample1() });
					input2.ShouldDeepEqual(new[] { ComplexModel2.CreateSample1() });
				})
				.Returns(new[] { ComplexModel1.CreateSample1() });
			var result = sampleServiceClient.PingComplexModelArray(new[] { ComplexModel1.CreateSample1() }, new[] { ComplexModel2.CreateSample1() });
			result.ShouldDeepEqual(new[] { ComplexModel1.CreateSample1() });
		}

		[Theory(Skip = "test not correct")]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestPingComplexArrayModelWithXmlArray(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.PingComplexModelArray(It.IsAny<ComplexModel1[]>(), It.IsAny<ComplexModel2[]>()))
				.Callback((ComplexModel1[] input, ComplexModel2[] input2) =>
				{
					input.ShouldDeepEqual(new[] { ComplexModel1.CreateSample1() });
					input2.ShouldDeepEqual(new[] { ComplexModel2.CreateSample1() });
				})
				.Returns(new[] { ComplexModel1.CreateSample1() });
			var result = sampleServiceClient.PingComplexModelArrayWithXmlArray(new[] { ComplexModel1.CreateSample1() }, new[] { ComplexModel2.CreateSample1() });
			result.ShouldDeepEqual(new[] { ComplexModel1.CreateSample1() });
		}

		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestPingStringArray(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			var data = new[] { "string", "string1" };

			_fixture.ServiceMock
				.Setup(x => x.PingStringArray(It.IsAny<string[]>()))
				.Callback((string[] input) => { input.ShouldDeepEqual(data); })
				.Returns(data);
			var result = sampleServiceClient.PingStringArray(data);
			result.ShouldDeepEqual(data);
		}

		[Theory(Skip = "test not correct")]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestPingStringArrayWithXmlArray(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			var data = new[] { "string", "string1" };

			_fixture.ServiceMock
				.Setup(x => x.PingStringArray(It.IsAny<string[]>()))
				.Callback((string[] input) => { input.ShouldDeepEqual(data); })
				.Returns(data);
			var result = sampleServiceClient.PingStringArrayWithXmlArray(data);
			result.ShouldDeepEqual(data);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestResponseIntArray(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);
			var data = new[] { 2, 5 };
			_fixture.ServiceMock.Setup(x => x.PingIntArray(data)).Callback((int[] input) => input.ShouldDeepEqual(data))
				.Returns(data);
			var result = sampleServiceClient.PingIntArray(data);
			result.ShouldDeepEqual(data);
		}

		//not compatible with DataContractSerializer
		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestPingComplexModelOutAndRefSerialization(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.PingComplexModelOutAndRef(
					It.IsAny<ComplexModel1>(),
					ref It.Ref<ComplexModel2>.IsAny,
					It.IsAny<ComplexObject>(),
					ref It.Ref<ComplexModel1>.IsAny,
					It.IsAny<ComplexObject>(),
					out It.Ref<ComplexModel2>.IsAny,
					out It.Ref<ComplexModel1>.IsAny))
				.Callback(new WcfXmlSerializationTests.PingComplexModelOutAndRefCallback(
					(
						ComplexModel1 inputModel_service,
						ref ComplexModel2 responseModelRef1_service,
						ComplexObject data1_service,
						ref ComplexModel1 responseModelRef2_service,
						ComplexObject data2_service,
						out ComplexModel2 responseModelOut1_service,
						out ComplexModel1 responseModelOut2_service) =>
					{
						// check input paremeters serialization
						inputModel_service.ShouldDeepEqual(ComplexModel1.CreateSample2());
						responseModelRef1_service.ShouldDeepEqual(ComplexModel2.CreateSample1());
						responseModelRef2_service.ShouldDeepEqual(ComplexModel1.CreateSample2());
						data1_service.ShouldDeepEqual(ComplexObject.CreateSample1());
						data2_service.ShouldDeepEqual(ComplexObject.CreateSample2());

						//sample response
						responseModelRef1_service = ComplexModel2.CreateSample2();
						responseModelRef2_service = ComplexModel1.CreateSample1();
						responseModelOut1_service = ComplexModel2.CreateSample3();
						responseModelOut2_service = ComplexModel1.CreateSample1();
					}))
				.Returns(true);

			var responseModelRef1_client = ComplexModel2.CreateSample1();
			var responseModelRef2_client = ComplexModel1.CreateSample2();

			var pingComplexModelOutAndRefResult_client =
				sampleServiceClient.PingComplexModelOutAndRef(
					ComplexModel1.CreateSample2(),
					ref responseModelRef1_client,
					ComplexObject.CreateSample1(),
					ref responseModelRef2_client,
					ComplexObject.CreateSample2(),
					out var responseModelOut1_client,
					out var responseModelOut2_client);

			// check output paremeters serialization
			pingComplexModelOutAndRefResult_client.ShouldBeTrue();
			responseModelRef1_client.ShouldDeepEqual(ComplexModel2.CreateSample2());
			responseModelRef2_client.ShouldDeepEqual(ComplexModel1.CreateSample1());
			responseModelOut1_client.ShouldDeepEqual(ComplexModel2.CreateSample3());
			responseModelOut2_client.ShouldDeepEqual(ComplexModel1.CreateSample1());
		}

		//not compatible with DataContractSerializer
		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestNotWrappedPropertyComplexInput(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.NotWrappedPropertyComplexInputRequestMethod(It.IsAny<NotWrappedPropertyComplexInputRequest>()))
				.Callback((NotWrappedPropertyComplexInputRequest request) =>
				{
					// Check deserialisation in service!
					request.NotWrappedComplexInput.ShouldNotBeNull();
					request.NotWrappedComplexInput.StringProperty.ShouldBe("z");
				})
				.Returns(() => new NotWrappedPropertyComplexInputResponse
				{
					NotWrappedComplexInput = new NotWrappedPropertyComplexInput
					{
						StringProperty = "z"
					}
				});

			var clientResponse = sampleServiceClient.NotWrappedPropertyComplexInputRequestMethod(new NotWrappedPropertyComplexInputRequest
			{
				NotWrappedComplexInput = new NotWrappedPropertyComplexInput
				{
					StringProperty = "z"
				}
			});

			clientResponse.ShouldNotBeNull();

			// The client does not support unpacking these message contracts, so further assertions have been
			// commented
			//	clientResponse.NotWrappedComplexInput.ShouldNotBeNull();
			//	clientResponse.NotWrappedComplexInput.StringProperty.ShouldBe("z");
		}

		//not compatible with DataContractSerializer
		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestNotWrappedFieldComplexInput(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.NotWrappedFieldComplexInputRequestMethod(It.IsAny<NotWrappedFieldComplexInputRequest>()))
				.Callback((NotWrappedFieldComplexInputRequest request) =>
				{
					// Check deserialisation in service!
					request.NotWrappedComplexInput.ShouldNotBeNull();
					request.NotWrappedComplexInput.StringProperty.ShouldBe("z");
				})
				.Returns(() => new NotWrappedFieldComplexInputResponse
				{
					NotWrappedComplexInput = new NotWrappedFieldComplexInput
					{
						StringProperty = "z"
					}
				});

			var clientResponse = sampleServiceClient.NotWrappedFieldComplexInputRequestMethod(new NotWrappedFieldComplexInputRequest
			{
				NotWrappedComplexInput = new NotWrappedFieldComplexInput
				{
					StringProperty = "z"
				}
			});

			clientResponse.ShouldNotBeNull();

			// The client does not support unpacking these message contracts, so further assertions have been
			// commented
			//	clientResponse.NotWrappedComplexInput.ShouldNotBeNull();
			//	clientResponse.NotWrappedComplexInput.StringProperty.ShouldBe("z");
		}

		//not compatible with DataContractSerializer
		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestNotWrappedFieldDoubleComplexInput(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.NotWrappedFieldDoubleComplexInputRequestMethod(It.IsAny<NotWrappedFieldDoubleComplexInputRequest>()))
				.Callback((NotWrappedFieldDoubleComplexInputRequest request) =>
				{
					// Check deserialisation in service!
					request.NotWrappedComplexInput1.ShouldNotBeNull();
					request.NotWrappedComplexInput1.StringProperty.ShouldBe("z");

					request.NotWrappedComplexInput2.ShouldNotBeNull();
					request.NotWrappedComplexInput2.StringProperty.ShouldBe("x");
				})
				.Returns(() => new NotWrappedFieldComplexInputResponse
				{
					NotWrappedComplexInput = new NotWrappedFieldComplexInput
					{
						StringProperty = "z"
					}
				});

			var clientResponse = sampleServiceClient.NotWrappedFieldDoubleComplexInputRequestMethod(new NotWrappedFieldDoubleComplexInputRequest
			{
				NotWrappedComplexInput1 = new NotWrappedFieldComplexInput
				{
					StringProperty = "z"
				},

				NotWrappedComplexInput2 = new NotWrappedFieldComplexInput
				{
					StringProperty = "x"
				}
			});

			clientResponse.ShouldNotBeNull();

			// The client does not support unpacking these message contracts, so further assertions have been
			// commented
			//	clientResponse.NotWrappedComplexInput.ShouldNotBeNull();
			//	clientResponse.NotWrappedComplexInput.StringProperty.ShouldBe("z");
		}

		//not compatible with DataContractSerializer
		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestPingComplexModelOldStyleSerialization(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.PingComplexModelOldStyle(It.IsAny<PingComplexModelOldStyleRequest>()))
				.Callback(
					(PingComplexModelOldStyleRequest request_service) =>
					{
						// check input paremeters serialization
						request_service.InputModel.ShouldDeepEqual(ComplexModel1.CreateSample2());
						request_service.ResponseModelRef1.ShouldDeepEqual(ComplexModel2.CreateSample1());
						request_service.ResponseModelRef2.ShouldDeepEqual(ComplexModel1.CreateSample2());
						request_service.Data1.ShouldDeepEqual(ComplexObject.CreateSample1());
						request_service.Data2.ShouldDeepEqual(ComplexObject.CreateSample2());
					})
				.Returns(
					() => new PingComplexModelOldStyleResponse
					{
						// sample response
						PingComplexModelOldStyleResult = true,
						ResponseModelRef1 = ComplexModel2.CreateSample2(),
						ResponseModelRef2 = ComplexModel1.CreateSample1(),
						ResponseModelOut1 = ComplexModel2.CreateSample3(),
						ResponseModelOut2 = ComplexModel1.CreateSample1()
					});

			var pingComplexModelOldStyleResult_client =
				sampleServiceClient.PingComplexModelOldStyle(
					new PingComplexModelOldStyleRequest
					{
						InputModel = ComplexModel1.CreateSample2(),
						ResponseModelRef1 = ComplexModel2.CreateSample1(),
						Data1 = ComplexObject.CreateSample1(),
						ResponseModelRef2 = ComplexModel1.CreateSample2(),
						Data2 = ComplexObject.CreateSample2()
					});

			// check output paremeters serialization
			pingComplexModelOldStyleResult_client
				.PingComplexModelOldStyleResult
				.ShouldBeTrue();
			pingComplexModelOldStyleResult_client
				.ResponseModelRef1
				.ShouldDeepEqual(ComplexModel2.CreateSample2());
			pingComplexModelOldStyleResult_client
				.ResponseModelRef2
				.ShouldDeepEqual(ComplexModel1.CreateSample1());
			pingComplexModelOldStyleResult_client
				.ResponseModelOut1
				.ShouldDeepEqual(ComplexModel2.CreateSample3());
			pingComplexModelOldStyleResult_client
				.ResponseModelOut2
				.ShouldDeepEqual(ComplexModel1.CreateSample1());
		}

		//not compatible with DataContractSerializer
		[Theory]
		[InlineData(SoapSerializer.XmlWcfSerializer)]
		public void TestEmptyParamsMethodSerialization(SoapSerializer soapSerializer)
		{
			var sampleServiceClient = _fixture.GetSampleServiceClient(soapSerializer);

			_fixture.ServiceMock
				.Setup(x => x.EmptyParamsMethod())
				.Returns(ComplexModel1.CreateSample2);

			var emptyParamsMethodResult_client =
				sampleServiceClient
					.EmptyParamsMethod();

			// check output paremeters serialization
			emptyParamsMethodResult_client.ShouldDeepEqual(ComplexModel1.CreateSample2());
		}
	}
}
