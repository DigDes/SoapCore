using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.MessageInspectors;
using SoapCore.Tests.MessageInspectors.MessageInspector2;

namespace SoapCore.Tests.Serialization.Issue1194
{
	[TestClass]
	public class Issue1194MessageHeaderTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://localhost:8051")
					.UseStartup<Startup>()
					.UseSetting("InspectorStyle", nameof(InspectorStyle.MessageInspector2NoException))
					.Build();

				host.Run();
			}).Wait(1000);
		}

		[TestInitialize]
		public void Reset()
		{
			MessageInspector2Mock.Reset();
		}

		public ITestService CreateClient(MessageVersion messageVersion = null)
		{
			messageVersion ??= MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None);

			var transportElement = new HttpTransportBindingElement
			{
				AuthenticationScheme = AuthenticationSchemes.Anonymous,
				KeepAliveEnabled = true,
				AllowCookies = false,
				MaxBufferPoolSize = 64 * 1024,
				MaxBufferSize = int.MaxValue,
				MaxReceivedMessageSize = int.MaxValue,
				TransferMode = TransferMode.Buffered,
			};

			var messageElement = new TextMessageEncodingBindingElement
			{
				MessageVersion = messageVersion,
				WriteEncoding = Encoding.UTF8,
				ReaderQuotas =
				{
					MaxDepth = 1024,
					MaxArrayLength = int.MaxValue,
					MaxBytesPerRead = 1024 * 128,

					//MaxNameTableCharCount = 200000,
					MaxStringContentLength = int.MaxValue
				}
			};

			var binding = new CustomBinding(messageElement, transportElement)
			{
				CloseTimeout = TimeSpan.FromSeconds(1),
				OpenTimeout = TimeSpan.FromSeconds(5),
				ReceiveTimeout = TimeSpan.FromSeconds(2),
				SendTimeout = TimeSpan.FromSeconds(3)
			};

			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:8051/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();

			return serviceClient;
		}

		[TestMethod]
		public void TestMessageHeadersDeserializedCorrectly()
		{
			Assert.IsFalse(MessageInspector2Mock.AfterReceivedRequestCalled);
			var client = CreateClient();

			var request = new TestRequestIn
			{
				TestHeader = new TestHeader
				{
					From = "Client",
					To = "Server"
				},
				TestRequest = new TestRequestType
				{
					TestIn = "Test"
				}
			};
			var result = client.Test(request);
			Assert.IsTrue(MessageInspector2Mock.AfterReceivedRequestCalled);

			Assert.AreEqual(request.TestHeader.To, result.TestHeader.From);
			Assert.AreEqual(request.TestHeader.From, result.TestHeader.To);
			Assert.AreEqual(request.TestRequest.TestIn + " result", result.TestResult.TestResult);

			var receivedMessage = MessageInspector2Mock.LastReceivedMessage;
			Assert.IsNotNull(receivedMessage);
#if NET5_0_OR_GREATER
			Assert.AreEqual("urn:test-namespace-binding:Test", receivedMessage.Headers.Action);
#endif
			Assert.AreEqual(2, receivedMessage.Headers.Count);
			var testHeader = receivedMessage.Headers.SingleOrDefault(h => h.Name == "TestHeader");
			Assert.IsNotNull(testHeader);
			Assert.AreEqual("urn:test-namespace.xms", testHeader.Namespace);

			// Convert message to XML and validate it per schema
			var msgSoap = SoapMsgToString(receivedMessage);

			var errors = ValidateSchema(msgSoap);
			Assert.AreEqual(0, errors.Count, string.Join('\n', errors.Select(error => error.Exception)));
		}

		[TestMethod]
		public void TestMessageHeadersDeserializedCorrectly_Soap11()
		{
			Assert.IsFalse(MessageInspector2Mock.AfterReceivedRequestCalled);
			var client = CreateClient(MessageVersion.CreateVersion(EnvelopeVersion.Soap11, AddressingVersion.None));

			var request = new TestRequestIn
			{
				TestHeader = new TestHeader
				{
					From = "Client",
					To = "Server"
				},
				TestRequest = new TestRequestType
				{
					TestIn = "Test"
				}
			};
			var result = client.Test(request);
			Assert.IsTrue(MessageInspector2Mock.AfterReceivedRequestCalled);

			Assert.AreEqual(request.TestHeader.To, result.TestHeader.From);
			Assert.AreEqual(request.TestHeader.From, result.TestHeader.To);
			Assert.AreEqual(request.TestRequest.TestIn + " result", result.TestResult.TestResult);

			var receivedMessage = MessageInspector2Mock.LastReceivedMessage;
			Assert.IsNotNull(receivedMessage);
			Assert.AreEqual(EnvelopeVersion.Soap11, receivedMessage.Version.Envelope);

			var testHeader = receivedMessage.Headers.SingleOrDefault(h => h.Name == "TestHeader");
			Assert.IsNotNull(testHeader);
			Assert.AreEqual("urn:test-namespace.xms", testHeader.Namespace);

			var msgSoap = SoapMsgToString(receivedMessage);

			Assert.IsTrue(
				msgSoap.Contains("http://schemas.xmlsoap.org/soap/envelope/"),
				"Rewritten message must be SOAP 1.1");
			Assert.IsFalse(msgSoap.Contains("http://www.w3.org/2003/05/soap-envelope"));

			if (msgSoap.Contains("mustUnderstand"))
			{
				Assert.IsTrue(
					msgSoap.Contains(":mustUnderstand="),
					"mustUnderstand must keep its SOAP namespace prefix (issue #1194)");
			}

			var errors = ValidateSchema(msgSoap);
			Assert.AreEqual(0, errors.Count, string.Join('\n', errors.Select(error => error.Exception)));
		}

		private static List<ValidationEventArgs> ValidateSchema(string msgSoap)
		{
			XmlSchemaSet schemas = new XmlSchemaSet
			{
				XmlResolver = null
			};

			const string myHeaderSchema = """
			                              <?xml version="1.0" encoding="UTF-8"?>
			                              <xsd:schema xmlns="urn:test-namespace.xms"
			                              xmlns:xsd="http://www.w3.org/2001/XMLSchema"
			                              targetNamespace="urn:test-namespace.xms"
			                              elementFormDefault="qualified">
			                              	<xsd:complexType name="TestHeader" final="#all">
			                              		<xsd:sequence>
			                              			<xsd:element name="From" type="xsd:string" nillable="false"/>
			                              			<xsd:element name="To" type="xsd:string" nillable="false"/>
			                              		</xsd:sequence>
			                              	</xsd:complexType>
			                              	<xsd:element name="TestHeader" type="TestHeader"/>
			                              </xsd:schema>
			                              """;

			using (var reader = new StringReader(myHeaderSchema))
			{
				schemas.Add(XmlSchema.Read(reader, null) !);
			}

			const string testMessageSchema = """
			                                 <?xml version="1.0" encoding="UTF-8"?>
			                                 <xsd:schema xmlns="urn:test-namespace.test" 
			                                 xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
			                                 targetNamespace="urn:test-namespace.test" 
			                                 elementFormDefault="qualified">
			                                 	<xsd:complexType name="TestRequestType" final="#all">
			                                 		<xsd:sequence>
			                                 			<xsd:element name="TestIn" type="xsd:string" nillable="false"/>
			                                 		</xsd:sequence>
			                                 	</xsd:complexType>
			                                 	<xsd:complexType name="TestResponseType" final="#all">
			                                 		<xsd:sequence>
			                                 			<xsd:element name="TestResult" type="xsd:string" nillable="false"/>
			                                 		</xsd:sequence>
			                                 	</xsd:complexType>
			                                 	<xsd:element name="TestRequest" type="TestRequestType" final="#all"/>
			                                 	<xsd:element name="TestResponse" type="TestResponseType" final="#all"/>
			                                 </xsd:schema>
			                                 """;

			using (var reader = new StringReader(testMessageSchema))
			{
				schemas.Add(XmlSchema.Read(reader, null) !);
			}

			var helper = new SoapSchemaValidationEventHandler();

			try
			{
				var xmlValidatorSettings = new XmlReaderSettings();
				xmlValidatorSettings.Schemas.Add(schemas);
				xmlValidatorSettings.ValidationType = ValidationType.Schema;
				xmlValidatorSettings.ValidationEventHandler += helper.ValidationEventHandler;

				using (TextReader tr = new StringReader(msgSoap))
				{
					using (var textReader = new XmlTextReader(tr))
					{
						using (var r = XmlReader.Create(textReader, xmlValidatorSettings))
						{
							bool read;

							do
							{
								read = r.Read();
							}
							while (read); // do nothing, just validate
						}
					}
				}
			}
			catch
			{
				// ignored
			}

			return helper.ErrorList;
		}

		private static string SoapMsgToString(Message receivedMessage)
		{
			var sb = new StringBuilder();

			try
			{
				using (var xml = XmlWriter.Create(sb, new XmlWriterSettings
				       {
					       Indent = true,
					       OmitXmlDeclaration = true,
					       CloseOutput = true,
					       CheckCharacters = true
				       }))
				{
					receivedMessage.WriteMessage(xml);
				}
			}
			catch (Exception)
			{
				sb.Clear();
			}

			return sb.ToString();
		}

		internal class SoapSchemaValidationEventHandler
		{
			private List<ValidationEventArgs> _errorList = new List<ValidationEventArgs>();

			public List<ValidationEventArgs> ErrorList
			{
				get { return _errorList; }
			}

			public void ValidationEventHandler(object sender, ValidationEventArgs e)
			{
				_errorList.Add(e);
			}
		}
	}
}
