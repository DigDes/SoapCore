using System;
using System.IO;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace SoapCore.Tests
{
	[TestClass]
	public class TrailingServicePathTunerTests
	{
		[TestMethod]
		public async Task TrailingServicePath_WritesMessage_True()
		{
			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware>();

			var encoder = new MockMessageEncoder();
			SoapOptions options = new SoapOptions()
			{
				Path = "/Service.svc",
				Binding = new CustomBinding(),
				MessageEncoder = encoder,
				ServiceType = typeof(SoapCore.TrailingServicePathTuner),
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
			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware>();

			var encoder = new MockMessageEncoder();
			SoapOptions options = new SoapOptions()
			{
				Path = "/v1/Service.svc",
				Binding = new CustomBinding(),
				MessageEncoder = encoder,
				ServiceType = typeof(SoapCore.TrailingServicePathTuner),
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
			// Arrange
			var logger = new NullLoggerFactory().CreateLogger<SoapEndpointMiddleware>();

			var encoder = new MockMessageEncoder();
			SoapOptions options = new SoapOptions()
			{
				Path = "/v1/Service.svc",
				Binding = new CustomBinding(),
				MessageEncoder = encoder,
				ServiceType = typeof(SoapCore.TrailingServicePathTuner),
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
