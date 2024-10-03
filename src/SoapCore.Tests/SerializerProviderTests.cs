using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
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
using SoapCore.Serializer;

namespace SoapCore.Tests
{
	[TestClass]
	public class SerializerProviderTests
	{
		[Timeout(500)]
		[TestMethod]
		public async Task UsingDefaultSerializer()
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

			await soapCore.Invoke(context, serviceCollection.BuildServiceProvider().CreateScope().ServiceProvider);

			// Assert
			Assert.IsTrue(context.Response.Body.Length > 0);
		}

		[Timeout(500)]
		[TestMethod]
		public async Task UsingCustomXMLSerializer()
		{
			// Arrange
			var logger = NullLoggerFactory.Instance.CreateLogger<SoapEndpointMiddleware<CustomMessage>>();

			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSoapCore();
			serviceCollection.AddSingleton<DenialOfServiceProofOfConcept>();
			serviceCollection.AddCustomSoapMessageSerializer<DenialOfServiceProofOfConceptRequestSerializer>();

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

			await soapCore.Invoke(context, serviceCollection.BuildServiceProvider().CreateScope().ServiceProvider);

			// Assert
			Assert.IsTrue(context.Response.Body.Length > 0);
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

		public class DenialOfServiceProofOfConceptRequestSerializer : IXmlSerializationHandler
		{
			public object DeserializeInputParameter(XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, ICustomAttributeProvider customAttributeProvider, IEnumerable<Type> knownTypes = null)
			{
				var serializer = new DataContractSerializer(parameterType, parameterName, parameterNs, (IEnumerable<Type>)knownTypes);
				return serializer.ReadObject(xmlReader, true);
			}
		}
	}
}
