using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
		public void TestMessageHeadersModelWithoutBody(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModel
			{
				Prop1 = "test"
			};

			_fixture.ServiceMock.Setup(x => x.Get(It.IsAny<MessageHeadersModel>())).Callback((MessageHeadersModel m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModel
			{
				Prop1 = model.Prop1
			});

			var result = service.Get(model);

			Assert.Equal(model.Prop1, result.Prop1);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithBodyAndNamespace(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModelWithBodyAndNamespace
			{
				Prop1 = Guid.NewGuid().ToString(),
				Body1 = Guid.NewGuid().ToString(),
				Body2 = Guid.NewGuid().ToString(),
				Prop2 = Guid.NewGuid().ToString()
			};

			_fixture.ServiceMock.Setup(x => x.GetWithBodyAndNamespace(It.IsAny<MessageHeadersModelWithBodyAndNamespace>())).Callback((MessageHeadersModelWithBodyAndNamespace m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModelWithBodyAndNamespace()
			{
				Prop1 = model.Prop1,
				Body1 = model.Body1,
				Body2 = model.Body2,
				Prop2 = model.Prop2
			});

			var result = service.GetWithBodyAndNamespace(model);

			Assert.Equal(model.Prop1, result.Prop1);
			Assert.Equal(model.Prop2, result.Prop2);
			Assert.Equal(model.Body1, result.Body1);
			Assert.Equal(model.Body2, result.Body2);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithBody(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModelWithBody
			{
				Prop1 = Guid.NewGuid().ToString(),
				Body1 = Guid.NewGuid().ToString(),
				Body2 = Guid.NewGuid().ToString(),
				Prop2 = Guid.NewGuid().ToString()
			};

			_fixture.ServiceMock.Setup(x => x.GetWithBody(It.IsAny<MessageHeadersModelWithBody>())).Callback((MessageHeadersModelWithBody m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModelWithBody()
			{
				Prop1 = model.Prop1,
				Body1 = model.Body1,
				Body2 = model.Body2,
				Prop2 = model.Prop2
			});

			var result = service.GetWithBody(model);

			Assert.Equal(model.Prop1, result.Prop1);
			Assert.Equal(model.Prop2, result.Prop2);
			Assert.Equal(model.Body1, result.Body1);
			Assert.Equal(model.Body2, result.Body2);
		}
	}
}
