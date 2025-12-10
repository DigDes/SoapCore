using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests
{
	[TestClass]
	public class HeadersHelperThreadingTest
	{
		[TestMethod]
		public void GetSoapAction_IsThreadSafe()
		{
			// Arrange
			const int totalOperations = 200_000;
			var exceptions = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

			// Act
			Parallel.For(0, totalOperations, (i, loopState) =>
			{
				try
				{
					var httpContext = new DefaultHttpContext();

					// Add variability to the action to ensure the parsing logic is stressed
					var action = $"http://test-uri.org/IService/TestAction{i % 100}";
					httpContext.Request.Headers["Content-Type"] = $"application/soap+xml; charset=utf-8; action=\"{action}\"";
					var message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "action");

					// The ref message is modified by GetSoapAction, so we need to pass it in a way that can be updated.
					var messageRef = new Message[1] { message };
					var soapAction = HeadersHelper.GetSoapAction(httpContext, ref messageRef[0]);

					// Assert
					Assert.AreEqual(action, soapAction);
				}
				catch (Exception ex)
				{
					exceptions.Enqueue(ex);
					loopState.Stop(); // Stop on first error to fail fast
				}
			});

			// Assert
			if (!exceptions.IsEmpty)
			{
				throw new AggregateException("One or more exceptions were thrown in worker threads.", exceptions);
			}
		}
	}
}
