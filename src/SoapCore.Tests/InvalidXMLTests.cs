using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests
{
	[TestClass]
	public class InvalidXMLTests
	{
		// https://github.com/DigDes/SoapCore/issues/610
		[Timeout(500)]
		[TestMethod]
		public async Task MissingNamespace()
		{
			// Arrange
			var logger = NullLoggerFactory.Instance.CreateLogger<SoapEndpointMiddleware<CustomMessage>>();

			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<DenialOfServiceProofOfConcept>();

			var options = new SoapOptions()
			{
				Path = "/Service.svc",
				EncoderOptions = new[]
				{
					new SoapEncoderOptions
					{
						MessageVersion = MessageVersion.Soap11,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					}
				},
				ServiceType = typeof(DenialOfServiceProofOfConcept),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.DataContractSerializer
			};

			var soapCore = new SoapEndpointMiddleware<CustomMessage>(logger, (innerContext) => Task.CompletedTask, options, serviceCollection.BuildServiceProvider());

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/Service.svc");
			context.Request.Method = "POST";
			context.Response.Body = new MemoryStream();

			// Act
			var request = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soapenc=""http://schemas.xmlsoap.org/soap/encoding/"" xmlns:tns=""https://dos.brianfeucht.com/"" xmlns:types=""https://dos.brianfeucht.com/encodedTypes"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body soap:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
    <tns:SpinTheThread>
      <a xsi:type=""xsd:string"">a</a>
      <b xsi:type=""xsd:string"">b</b>
    </tns:SpinTheThread>
  </soap:Body>
</soap:Envelope>";
			context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(request), false);
			context.Request.ContentType = "text/xml; charset=utf-8";

			await soapCore.Invoke(context);

			// Assert
			Assert.IsTrue(context.Response.Body.Length > 0);
		}

		[TestMethod]
		public async Task DuplicatedElement()
		{
			// Arrange
			var logger = NullLoggerFactory.Instance.CreateLogger<SoapEndpointMiddleware<CustomMessage>>();

			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<DuplicatedElementService>();
			serviceCollection.AddSoapCore();

			var options = new SoapOptions()
			{
				Path = "/Service.asmx",
				EncoderOptions = new[]
				{
					new SoapEncoderOptions
					{
						MessageVersion = MessageVersion.Soap11,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					}
				},
				ServiceType = typeof(DuplicatedElementService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.XmlSerializer
			};

			var serviceProvider = serviceCollection.BuildServiceProvider();

			var soapCore = new SoapEndpointMiddleware<CustomMessage>(logger, (innerContext) => Task.CompletedTask, options, serviceProvider);

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/Service.asmx");
			context.Request.Method = "POST";
			context.Response.Body = new MemoryStream();

			// Act
			var request = @"
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:rlx=""https://dos.brianfeucht.com/"">
    <soapenv:Body>
    <rlx:Test>
		<eventRef>a</eventRef>
		<eventRef>b</eventRef>
		<other>c</other>
    </rlx:Test>
  </soapenv:Body>
</soapenv:Envelope>";
			context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(request), false);
			context.Request.ContentType = "text/xml; charset=utf-8";

			await soapCore.Invoke(context);

			// Assert
			context.Response.Body.Seek(0, SeekOrigin.Begin);
			using var response = new StreamReader(context.Response.Body, Encoding.UTF8);
			var body = await response.ReadToEndAsync();
			Assert.IsTrue(body.Contains("<TestResult>a c</TestResult>"));
		}

		[DataTestMethod]
		[DataRow("application/soap+xml; charset=utf-8; action=\"Request\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"Request/\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"testRequest/\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"TESTRequest/\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"/\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"https://dos.brianfeucht.com/test/\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"https://dos.brianfeucht.com/test\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"https://dos.brianfeucht.com/TEST\"")]
		[DataRow("application/soap+xml; charset=utf-8; action=\"https://dos.brianfeucht.com/Test\"")]
		public async Task InvalidSoapActionContentType(string contentType)
		{
			// Arrange
			var logger = NullLoggerFactory.Instance.CreateLogger<SoapEndpointMiddleware<CustomMessage>>();

			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<DuplicatedElementService>();
			serviceCollection.AddSoapCore();

			var options = new SoapOptions()
			{
				Path = "/Service.asmx",
				EncoderOptions = new[]
				{
					new SoapEncoderOptions
					{
						MessageVersion = MessageVersion.Soap12WSAddressing10,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					}
				},
				ServiceType = typeof(DuplicatedElementService),
				SoapModelBounder = new MockModelBounder(),
				SoapSerializer = SoapSerializer.XmlSerializer
			};

			var soapCore = new SoapEndpointMiddleware<CustomMessage>(logger, _ => Task.CompletedTask, options, serviceCollection.BuildServiceProvider());

			var context = new DefaultHttpContext();
			context.Request.Path = new PathString("/Service.asmx");
			context.Request.Method = "POST";
			context.Response.Body = new MemoryStream();

			// Act
			var request = @"
<soap12:Envelope xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"" xmlns:rlx=""https://dos.brianfeucht.com/"">
    <soap12:Body>
    <rlx:Test>
		<eventRef>a</eventRef>
		<other>c</other>
    </rlx:Test>
  </soap12:Body>
</soap12:Envelope>";
			context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(request), false);
			context.Request.ContentType = contentType;

			await soapCore.Invoke(context);

			// Assert
			context.Response.Body.Seek(0, SeekOrigin.Begin);
			using var response = new StreamReader(context.Response.Body, Encoding.UTF8);
			var body = await response.ReadToEndAsync();
			Assert.IsTrue(body.Contains("<TestResult>a c</TestResult>"));
		}

		[ServiceContract(Namespace = "https://dos.brianfeucht.com/")]
		public class DenialOfServiceProofOfConcept
		{
			[OperationContract]
			public Task<string> SpinTheThread(string a, string b)
			{
				return Task.FromResult("Hello World");
			}
		}

		[ServiceContract(Namespace = "https://dos.brianfeucht.com/")]
		public class DuplicatedElementService
		{
			[OperationContract]
			public Task<string> Test(string eventRef, string other)
			{
				return Task.FromResult(eventRef + " " + other);
			}
		}
	}
}
