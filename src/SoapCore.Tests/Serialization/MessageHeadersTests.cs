using System;
using DeepEqual.Syntax;
using Moq;
using SoapCore.Tests.Serialization.Models.DataContract;
using Xunit;

namespace SoapCore.Tests.Serialization
{
	[Collection("serialization")]
	public class MessageHeadersTests : IClassFixture<ServiceFixture<ISampleServiceWithMessageHeaders>>
	{
		private readonly ServiceFixture<ISampleServiceWithMessageHeaders> _fixture;

		public MessageHeadersTests(ServiceFixture<ISampleServiceWithMessageHeaders> fixture)
		{
			_fixture = fixture;
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithBody(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModelWithBody
			{
				Prop1 = Guid.NewGuid().ToString(),
				Prop2 = Guid.NewGuid().ToString(),
				Prop3 = Guid.NewGuid().ToString(),
				Prop4 = Guid.NewGuid().ToString(),
				Prop5 = Guid.NewGuid().ToString(),
				Prop6 = Guid.NewGuid().ToString(),
				Prop7 = Guid.NewGuid().ToString(),
				Prop8 = Guid.NewGuid().ToString(),
				Body1 = Guid.NewGuid().ToString(),
				Body2 = Guid.NewGuid().ToString()
			};

			_fixture.ServiceMock.Setup(x => x.GetWithBody(It.IsAny<MessageHeadersModelWithBody>())).Callback((MessageHeadersModelWithBody m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModelWithBody()
			{
				Prop1 = model.Prop1,
				Prop2 = model.Prop2,
				Prop3 = model.Prop3,
				Prop4 = model.Prop4,
				Prop5 = model.Prop5,
				Prop6 = model.Prop6,
				Prop7 = model.Prop7,
				Prop8 = model.Prop8,
				Body1 = model.Body1,
				Body2 = model.Body2
			});

			var result = service.GetWithBody(model);

			Assert.Equal(model.Prop1, result.Prop1);
			Assert.Equal(model.Prop2, result.Prop2);
			Assert.Equal(model.Prop3, result.Prop3);
			Assert.Equal(model.Prop4, result.Prop4);
			Assert.Equal(model.Prop5, result.Prop5);
			Assert.Equal(model.Prop6, result.Prop6);
			Assert.Equal(model.Prop7, result.Prop7);
			Assert.Equal(model.Prop8, result.Prop8);
			Assert.Equal(model.Body1, result.Body1);
			Assert.Equal(model.Body2, result.Body2);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithBodyAndNamespace(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModelWithBodyAndNamespace
			{
				Prop1 = Guid.NewGuid().ToString(),
				Prop2 = Guid.NewGuid().ToString(),
				Prop3 = Guid.NewGuid().ToString(),
				Prop4 = Guid.NewGuid().ToString(),
				Prop5 = Guid.NewGuid().ToString(),
				Prop6 = Guid.NewGuid().ToString(),
				Prop7 = Guid.NewGuid().ToString(),
				Prop8 = Guid.NewGuid().ToString(),
				Body1 = Guid.NewGuid().ToString(),
				Body2 = Guid.NewGuid().ToString()
			};

			_fixture.ServiceMock.Setup(x => x.GetWithBodyAndNamespace(It.IsAny<MessageHeadersModelWithBodyAndNamespace>())).Callback((MessageHeadersModelWithBodyAndNamespace m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModelWithBodyAndNamespace()
			{
				Prop1 = model.Prop1,
				Prop2 = model.Prop2,
				Prop3 = model.Prop3,
				Prop4 = model.Prop4,
				Prop5 = model.Prop5,
				Prop6 = model.Prop6,
				Prop7 = model.Prop7,
				Prop8 = model.Prop8,
				Body1 = model.Body1,
				Body2 = model.Body2
			});

			var result = service.GetWithBodyAndNamespace(model);

			Assert.Equal(model.Prop1, result.Prop1);
			Assert.Equal(model.Prop2, result.Prop2);
			Assert.Equal(model.Prop3, result.Prop3);
			Assert.Equal(model.Prop4, result.Prop4);
			Assert.Equal(model.Prop5, result.Prop5);
			Assert.Equal(model.Prop6, result.Prop6);
			Assert.Equal(model.Prop7, result.Prop7);
			Assert.Equal(model.Prop8, result.Prop8);
			Assert.Equal(model.Body1, result.Body1);
			Assert.Equal(model.Body2, result.Body2);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithNamespace(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModelWithNamespace
			{
				Prop1 = Guid.NewGuid().ToString(),
				Prop2 = Guid.NewGuid().ToString(),
				Prop3 = Guid.NewGuid().ToString(),
				Prop4 = Guid.NewGuid().ToString()
			};

			_fixture.ServiceMock.Setup(x => x.GetWithNamespace(It.IsAny<MessageHeadersModelWithNamespace>())).Callback((MessageHeadersModelWithNamespace m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModelWithNamespace
			{
				Prop1 = model.Prop1,
				Prop2 = model.Prop2,
				Prop3 = model.Prop3,
				Prop4 = model.Prop4
			});

			var result = service.GetWithNamespace(model);

			Assert.Equal(model.Prop1, result.Prop1);
			Assert.Equal(model.Prop2, result.Prop2);
			Assert.Equal(model.Prop3, result.Prop3);
			Assert.Equal(model.Prop4, result.Prop4);
		}
	}
}
