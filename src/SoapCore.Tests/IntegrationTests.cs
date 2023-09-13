using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Model;
using SoapCore.Tests.Utilities;

namespace SoapCore.Tests
{
	[TestClass]
	public class IntegrationTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext testContext)
		{
			Task.Run(() =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls("http://localhost:5050")
					.UseStartup<Startup>()
					.Build();

				host.Run();
			}).Wait(1000);
		}

		[TestMethod]
		public void PingWithCaseInsensitivePath()
		{
			var client = CreateClient(caseInsensitivePath: true);
			var result = client.Ping("hello, world");
			Assert.AreEqual("hello, world", result);
		}

		[TestMethod]
		public void PingSoap12()
		{
			var client = CreateSoap12Client();
			var result = client.Ping("hello, world");
			Assert.AreEqual("hello, world", result);
		}

		[TestMethod]
		public void PingSoap11Iso55891()
		{
			var client = CreateSoap11Iso88591Client();
			var result = client.Ping("hello, world");
			Assert.AreEqual("hello, world", result);
		}

		[TestMethod]
		public void Ping()
		{
			var client = CreateClient();
			var result = client.Ping("hello, world");
			Assert.AreEqual("hello, world", result);
		}

		[TestMethod]
		public void EmptyArgs()
		{
			var client = CreateClient();
			var result = client.EmptyArgs();
			Assert.AreEqual("EmptyArgs", result);
		}

		[TestMethod]
		public void EmptyArgsASMX()
		{
			var client = CreateClientASMX();
			var result = client.EmptyArgs();
			Assert.AreEqual("EmptyArgs", result);
		}

		[TestMethod]
		public void SingleInt()
		{
			var client = CreateClient();
			var result = client.SingleInteger(5);
			Assert.AreEqual("5", result);
		}

		[TestMethod]
		public void AsyncMethod()
		{
			var client = CreateClient();
			var result = client.AsyncMethod().Result;
			Assert.AreEqual("hello, async", result);
		}

		[TestMethod]
		public void Nullable()
		{
			var client = CreateClient();
			Assert.IsFalse(client.IsNull(5.0d));
			Assert.IsTrue(client.IsNull(null));
		}

		[TestMethod]
		public void OverloadedMethod()
		{
			var client = CreateClient();
			Assert.AreEqual("Overload(double)", client.Overload(5.0d));
			Assert.AreEqual("Overload(string)", client.Overload("hello, world"));
		}

		[TestMethod]
		public void OperationNameOverride()
		{
			var client = CreateClient();
			Assert.IsTrue(client.OperationName());
		}

		[TestMethod]
		public void OutParam()
		{
			var client = CreateClient();
			string message;
			client.OutParam(out message);
			Assert.AreEqual("hello, world", message);
		}

		[TestMethod]
		public void OutComplexParam()
		{
			var client = CreateClient();
			client.OutComplexParam(out ComplexModelInput test);
			Assert.AreEqual(test.IntProperty, 10);
		}

		[TestMethod]
		public void RefParam()
		{
			var client = CreateClient();
			string message = string.Empty;
			client.RefParam(ref message);
			Assert.AreEqual("hello, world", message);
		}

		[TestMethod]
		public void ArrayInput()
		{
			var client = CreateClient();
			List<ComplexModelInput> complexModelInputs = new List<ComplexModelInput>();
			complexModelInputs.Add(new ComplexModelInput());
			var e = client.ArrayOfComplexItems(complexModelInputs.ToArray());
			Assert.AreEqual(e.Length, complexModelInputs.Count);
		}

		[TestMethod]
		public void ListInput()
		{
			var client = CreateClient();
			List<ComplexModelInput> complexModelInputs = new List<ComplexModelInput>();
			complexModelInputs.Add(new ComplexModelInput());
			var e = client.ListOfComplexItems(complexModelInputs);
			Assert.AreEqual(e.Count, complexModelInputs.Count);
		}

		[TestMethod]
		public void DictionaryInput()
		{
			var client = CreateClient();
			Dictionary<string, string> dictionaryInputs = new Dictionary<string, string>();
			dictionaryInputs.Add("1", "2");
			var e = client.ListOfDictionaryItems(dictionaryInputs);
			Assert.AreEqual(e["1"], dictionaryInputs["1"]);
			Assert.AreEqual(e.Count, dictionaryInputs.Count);
		}

		[TestMethod]
		[DataRow(typeof(ComplexInheritanceModelInputA))]
		[DataRow(typeof(ComplexInheritanceModelInputB))]
		public void GetComplexInheritanceModel(Type type)
		{
			var client = CreateClient();
			var input = (ComplexInheritanceModelInputBase)Activator.CreateInstance(type);
			var output = client.GetComplexInheritanceModel(input);
			Assert.AreEqual(input.GetType(), output.GetType());
		}

		[TestMethod]
		public void ComplexModelInputFromServiceKnownType()
		{
			var client = CreateClient();
			var input = new ComplexModelInput
			{
				IntProperty = 123,
				StringProperty = "Test string",
			};
			var output = client.ComplexModelInputFromServiceKnownType(input);
			Assert.AreEqual(input.IntProperty, output.IntProperty);
			Assert.AreEqual(input.StringProperty, output.StringProperty);
		}

		[TestMethod]
		public void ObjectFromServiceKnownType()
		{
			var client = CreateClient();
			var input = new ComplexModelInput
			{
				IntProperty = 123,
				StringProperty = "Test string",
			};
			var output = client.ObjectFromServiceKnownType(input);
			Assert.IsInstanceOfType(output, typeof(ComplexModelInput));
			Assert.AreEqual(input.IntProperty, ((ComplexModelInput)output).IntProperty);
			Assert.AreEqual(input.StringProperty, ((ComplexModelInput)output).StringProperty);
		}

		[TestMethod]
		public void ComplexModelInputFromServiceKnownTypeProvider()
		{
			var client = CreateClient();
			var input = new ComplexModelInput()
			{
				StringProperty = "test"
			};
			var output = client.GetComplexModelInputFromKnownTypeProvider(input);
			Assert.IsInstanceOfType(output, typeof(ComplexTreeModelInput));
			Assert.AreEqual(input.StringProperty, output.Item.StringProperty);
		}

		[TestMethod]
		public void ReturnXmlElement()
		{
			var client = CreateClient();
			var output = client.ReturnXmlElement();
			Assert.IsInstanceOfType(output, typeof(XmlElement));
			Assert.AreEqual(output.OuterXml, "<TestXml xmlns=\"\" />");
		}

		[TestMethod]
		public void XmlElemetInput()
		{
			var client = CreateClient();
			XmlDocument xdInput = new XmlDocument();
			xdInput.LoadXml("<XmlTestInput/>");
			var output = client.XmlElementInput(xdInput.DocumentElement);
			Assert.IsInstanceOfType(output, typeof(XmlElement));
			Assert.IsTrue(output.OuterXml.Contains("Success"));
		}

		[TestMethod]
		public void ThrowsFaultException()
		{
			var client = CreateClient();
			Assert.ThrowsException<FaultException>(() =>
			{
				client.ThrowException();
			});
		}

		[TestMethod]
		public void ExceptionMessage()
		{
			var client = CreateClient();
			var e = Assert.ThrowsException<FaultException>(() =>
			{
				client.ThrowExceptionWithMessage("Your error message here");
			});
			Assert.AreEqual("Your error message here", e.Message);
		}

		[TestMethod]
		public void ExceptionMessageSoap12()
		{
			var client = CreateSoap12Client();

			var e = Assert.ThrowsException<FaultException>(() =>
			{
				client.ThrowExceptionWithMessage("Your error message here");
			});

			Assert.AreEqual("Your error message here", e.Message);
		}

		[TestMethod]
		public void ExceptionMessageSoap11iso88591()
		{
			var client = CreateSoap11Iso88591Client();

			var e = Assert.ThrowsException<FaultException>(() =>
			{
				client.ThrowExceptionWithMessage("Your error message here");
			});

			Assert.AreEqual("Your error message here", e.Message);
		}

		[TestMethod]
		public void ThrowsDetailedFault()
		{
			var client = CreateClient();
			var e = Assert.ThrowsException<FaultException<FaultDetail>>(() =>
			{
				client.ThrowDetailedFault("Detail message");
			});
			Assert.IsNotNull(e.Detail);
			Assert.AreEqual("Detail message", e.Detail.ExceptionProperty);
		}

		[TestMethod]
		public void ThrowsDetailedSoap12Fault()
		{
			var client = CreateSoap12Client();

			var e = Assert.ThrowsException<FaultException<FaultDetail>>(() =>
			{
				client.ThrowDetailedFault("Detail message");
			});

			Assert.IsNotNull(e.Detail);
			Assert.AreEqual("Detail message", e.Detail.ExceptionProperty);
		}

		[TestMethod]
		public void EmptyBody()
		{
			var client = CreateClientASMX();
			EmptyMembers empty_members = new EmptyMembers();
			var result = client.EmpryBody(null);
			Assert.AreEqual("OK", result);
		}

		private ITestService CreateClient(bool caseInsensitivePath = false)
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(
				string.Format("http://{0}:5050/{1}.svc", "localhost", caseInsensitivePath ? "serviceci" : "Service")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		private ITestService CreateClientASMX(bool caseInsensitivePath = false)
		{
			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri(
				string.Format("http://{0}:5050/{1}.asmx", "localhost", caseInsensitivePath ? "serviceci" : "Service")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		private ITestService CreateSoap12Client()
		{
			var transport = new HttpTransportBindingElement();
			var textencoding = new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, Encoding.UTF8);
			var binding = new CustomBinding(textencoding, transport);
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5050/Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}

		private ITestService CreateSoap11Iso88591Client()
		{
			var transport = new HttpTransportBindingElement();
			var textencoding = new CustomTextMessageBindingElement("iso-8859-1", "text/xml", MessageVersion.Soap11);
			var binding = new CustomBinding(textencoding, transport);
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5050/WSA11ISO88591Service.svc", "localhost")));
			var channelFactory = new ChannelFactory<ITestService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			return serviceClient;
		}
	}
}
