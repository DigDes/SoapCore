using System.ServiceModel.Channels;
using System.Threading.Tasks;
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
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware>();

			var encoder = new MockMessageEncoder();
			SoapOptions options = new SoapOptions()
			{
				Path = "/Service.svc", // this is the path registered in app startup
				Binding = new CustomBinding(),
				MessageEncoders = new MessageEncoder[] { encoder },
				ServiceType = typeof(MockSoapService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.DataContractSerializer
			};

			SoapEndpointMiddleware soapCore = new SoapEndpointMiddleware(logger, (innerContext) => Task.FromResult(TaskStatus.RanToCompletion), options);

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/DynamicPath/Service.svc");

			// Act
			// MockServiceProvider(false) simulates registering the TrailingServicePathTuner in app startup
			await soapCore.Invoke(context, new MockServiceProvider(true));

			// Assert
			Assert.AreEqual(true, encoder.DidWriteMessage);
		}

		[TestMethod]
		public async Task TrailingServicePath_WritesMessage_False()
		{
			// The Trailing Service Path Tuner is an opt-in service.

			// This test demonstrates the breaking behavior when the service
			// is registered in app startup but a single path-part is not implemented.

			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware>();

			var encoder = new MockMessageEncoder();
			SoapOptions options = new SoapOptions()
			{
				Path = "/v1/Service.svc", // this is the multi-part path registered in app startup
				Binding = new CustomBinding(),
				MessageEncoders = new MessageEncoder[] { encoder },
				ServiceType = typeof(MockSoapService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.DataContractSerializer
			};

			SoapEndpointMiddleware soapCore = new SoapEndpointMiddleware(logger, (innerContext) => Task.FromResult(TaskStatus.RanToCompletion), options);

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/DynamicPath/Service.svc");

			// Act
			// MockServiceProvider(false) simulates registering the TrailingServicePathTuner in app startup
			await soapCore.Invoke(context, new MockServiceProvider(true));

			// Assert
			Assert.AreEqual(false, encoder.DidWriteMessage);
		}

		[TestMethod]
		public async Task FullPath_WritesMessage_True()
		{
			// The Trailing Service Path Tuner is an opt-in service.

			// This test demonstrates existing functionality is not changed
			// when the service is not registered in app startup (opted-in).

			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware>();

			var encoder = new MockMessageEncoder();
			SoapOptions options = new SoapOptions()
			{
				Path = "/v1/Service.svc", // this is the multi-part path registered in app startup
				Binding = new CustomBinding(),
				MessageEncoders = new MessageEncoder[] { encoder },
				ServiceType = typeof(MockSoapService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.DataContractSerializer
			};

			SoapEndpointMiddleware soapCore = new SoapEndpointMiddleware(logger, (innerContext) => Task.FromResult(TaskStatus.RanToCompletion), options);

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/v1/Service.svc");

			// Act
			// MockServiceProvider(false) simulates not registering the TrailingServicePathTuner in app startup
			await soapCore.Invoke(context, new MockServiceProvider(false));

			// Assert
			Assert.AreEqual(true, encoder.DidWriteMessage);
		}
	}
}
