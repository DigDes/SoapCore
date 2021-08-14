using System.IO;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests
{
	[TestClass]
	public class TrailingServicePathTunerTests
	{
		[TestMethod]
		public async Task TrailingServicePath_WritesMessage_True()
		{
			// The Trailing Service Path Tuner is an opt-in service.

			// This test demonstrates the proper behavior when the service
			// is registered in app startup.

			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware<CustomMessage>>();

			SoapOptions options = new SoapOptions()
			{
				Path = "/Service.svc", // this is the path registered in app startup
				EncoderOptions = new[]
				{
					new SoapEncoderOptions
					{
						MessageVersion = MessageVersion.Soap12WSAddressing10,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					}
				},
				ServiceType = typeof(MockSoapService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.DataContractSerializer
			};

			SoapEndpointMiddleware<CustomMessage> soapCore = new SoapEndpointMiddleware<CustomMessage>(logger, (innerContext) => Task.FromResult(TaskStatus.RanToCompletion), options);

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/DynamicPath/Service.svc");
			context.Request.Method = "GET";
			context.Response.Body = new MemoryStream();

			// Act
			// MockServiceProvider(false) simulates registering the TrailingServicePathTuner in app startup
			await soapCore.Invoke(context, new MockServiceProvider(true));

			// Assert
			Assert.IsTrue(context.Response.Body.Length > 0);
		}

		[TestMethod]
		public async Task TrailingServicePath_WritesMessage_False()
		{
			// The Trailing Service Path Tuner is an opt-in service.

			// This test demonstrates the breaking behavior when the service
			// is registered in app startup but a single path-part is not implemented.

			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware<CustomMessage>>();

			SoapOptions options = new SoapOptions()
			{
				Path = "/v1/Service.svc", // this is the multi-part path registered in app startup
				EncoderOptions = new[]
				{
					new SoapEncoderOptions
					{
						MessageVersion = MessageVersion.Soap12WSAddressing10,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					}
				},
				ServiceType = typeof(MockSoapService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.DataContractSerializer
			};

			SoapEndpointMiddleware<CustomMessage> soapCore = new SoapEndpointMiddleware<CustomMessage>(logger, (innerContext) => Task.FromResult(TaskStatus.RanToCompletion), options);

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/DynamicPath/Service.svc");
			context.Response.Body = new MemoryStream();

			// Act
			// MockServiceProvider(false) simulates registering the TrailingServicePathTuner in app startup
			await soapCore.Invoke(context, new MockServiceProvider(true));

			// Assert
			Assert.IsFalse(context.Response.Body.Length > 0);
		}

		[TestMethod]
		public async Task FullPath_WritesMessage_True()
		{
			// The Trailing Service Path Tuner is an opt-in service.

			// This test demonstrates existing functionality is not changed
			// when the service is not registered in app startup (opted-in).

			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware<CustomMessage>>();

			SoapOptions options = new SoapOptions()
			{
				Path = "/v1/Service.svc", // this is the multi-part path registered in app startup
				EncoderOptions = new[]
				{
					new SoapEncoderOptions
					{
						MessageVersion = MessageVersion.Soap12WSAddressing10,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					}
				},
				ServiceType = typeof(MockSoapService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.DataContractSerializer
			};

			SoapEndpointMiddleware<CustomMessage> soapCore = new SoapEndpointMiddleware<CustomMessage>(logger, (innerContext) => Task.FromResult(TaskStatus.RanToCompletion), options);

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/v1/Service.svc");
			context.Request.Method = "GET";
			context.Response.Body = new MemoryStream();

			// Act
			// MockServiceProvider(false) simulates not registering the TrailingServicePathTuner in app startup
			await soapCore.Invoke(context, new MockServiceProvider(false));

			// Assert
			Assert.IsTrue(context.Response.Body.Length > 0);
		}
	}
}
