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
	public class MessageHeadersTests2 : IClassFixture<ServiceFixture<ISampleServiceWithMessageHeaders2>>
	{
		private readonly ServiceFixture<ISampleServiceWithMessageHeaders2> _fixture;

		public MessageHeadersTests2(ServiceFixture<ISampleServiceWithMessageHeaders2> fixture)
		{
			_fixture = fixture;
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithoutBody2(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModel2
			{
				Prop1 = "test"
			};

			_fixture.ServiceMock.Setup(x => x.Get2(It.IsAny<MessageHeadersModel2>())).Callback((MessageHeadersModel2 m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModel2
			{
				Prop1 = model.Prop1
			});

			var result = service.Get2(model);

			Assert.Equal(model.Prop1, result.Prop1);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithBodyAndNamespace2(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModelWithBodyAndNamespace2
			{
				Prop1 = Guid.NewGuid().ToString(),
				Body1 = Guid.NewGuid().ToString(),
				Body2 = Guid.NewGuid().ToString(),
				Prop2 = Guid.NewGuid().ToString()
			};

			_fixture.ServiceMock.Setup(x => x.GetWithBodyAndNamespace2(It.IsAny<MessageHeadersModelWithBodyAndNamespace2>())).Callback((MessageHeadersModelWithBodyAndNamespace2 m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModelWithBodyAndNamespace2()
			{
				Prop1 = model.Prop1,
				Body1 = model.Body1,
				Body2 = model.Body2,
				Prop2 = model.Prop2
			});

			var result = service.GetWithBodyAndNamespace2(model);

			Assert.Equal(model.Prop1, result.Prop1);
			Assert.Equal(model.Prop2, result.Prop2);
			Assert.Equal(model.Body1, result.Body1);
			Assert.Equal(model.Body2, result.Body2);
		}

		[Theory]
		[InlineData(SoapSerializer.XmlSerializer)]
		public void TestMessageHeadersModelWithBody2(SoapSerializer serializer)
		{
			var service = _fixture.GetSampleServiceClient(serializer);
			var model = new MessageHeadersModelWithBody2
			{
				Prop1 = Guid.NewGuid().ToString(),
				Body1 = Guid.NewGuid().ToString(),
				Body2 = Guid.NewGuid().ToString(),
				Prop2 = Guid.NewGuid().ToString()
			};

			_fixture.ServiceMock.Setup(x => x.GetWithBody2(It.IsAny<MessageHeadersModelWithBody2>())).Callback((MessageHeadersModelWithBody2 m) =>
			{
				m.ShouldDeepEqual(model);
			}).Returns(new MessageHeadersModelWithBody2()
			{
				Prop1 = model.Prop1,
				Body1 = model.Body1,
				Body2 = model.Body2,
				Prop2 = model.Prop2
			});

			var result = service.GetWithBody2(model);

			Assert.Equal(model.Prop1, result.Prop1);
			Assert.Equal(model.Prop2, result.Prop2);
			Assert.Equal(model.Body1, result.Body1);
			Assert.Equal(model.Body2, result.Body2);
		}
	}
}
