using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.MessageContract.Models;

namespace SoapCore.Tests.MessageContract
{
	// Tests for https://github.com/DigDes/SoapCore/issues/1161.
	// When an endpoint uses SoapSerializer.XmlSerializer, response headers must
	// also use XmlSerializer. DataContractSerializer would either produce the
	// wrong XML, or throw InvalidDataContractException if the header type contains
	// an [XmlAnyAttribute] XmlAttribute[] member with values.
	[TestClass]
	public class Issue1161Tests
	{
		private const string DefaultNamespacedHeaderRequest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Header>
    <MessageHeader xmlns=""http://www.ebxml.org/namespaces/messageHeader"">
      <From>SomeSender</From>
    </MessageHeader>
  </s:Header>
  <s:Body>
   <Process xmlns=""http://tempuri.org/"">
      <BodyMessage>someBody</BodyMessage>
    </Process>
  </s:Body>
 </s:Envelope>";

		private const string PrefixedNamespacedHeaderRequest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Header>
    <h:MessageHeader xmlns:h=""http://www.ebxml.org/namespaces/messageHeader""
      xmlns=""http://www.ebxml.org/namespaces/messageHeader"">
      <From>SomeSender</From>
    </h:MessageHeader>
  </s:Header>
  <s:Body>
   <Process xmlns=""http://tempuri.org/"">
      <BodyMessage>someBody</BodyMessage>
    </Process>
  </s:Body>
 </s:Envelope>";

		[TestMethod]
		public async Task Issue1161_DefaultNamespaceHeader_DoesNotEmitDataContractSerializerNamespaces()
		{
			var response = await PostSoapAsync(DefaultNamespacedHeaderRequest);

			// On an XmlSerializer endpoint, response headers must use XmlSerializer.
			// DataContractSerializer would add unwanted namespaces and an extra
			// <SomeOtherAttributes /> element. Neither should appear in the output.
			StringAssert.Contains(response, "<From>SomeSender</From>");
			StringAssert.DoesNotMatch(response, new System.Text.RegularExpressions.Regex("schemas\\.datacontract\\.org/2004/07"));
			StringAssert.DoesNotMatch(response, new System.Text.RegularExpressions.Regex("Serialization/Arrays"));
			StringAssert.DoesNotMatch(response, new System.Text.RegularExpressions.Regex("<SomeOtherAttributes"));
		}

		[TestMethod]
		public async Task Issue1161_PrefixedNamespaceHeader_DoesNotThrowInvalidDataContractException()
		{
			// A namespace-prefixed inbound header (xmlns:h="...") populates the
			// [XmlAnyAttribute] XmlAttribute[] member during deserialization.
			// The response must serialize successfully (no InvalidDataContractException).
			var response = await PostSoapAsync(PrefixedNamespacedHeaderRequest);

			StringAssert.Contains(response, "<From>SomeSender</From>");
			StringAssert.DoesNotMatch(response, new System.Text.RegularExpressions.Regex("InvalidDataContractException"));
			StringAssert.DoesNotMatch(response, new System.Text.RegularExpressions.Regex("schemas\\.datacontract\\.org/2004/07"));
		}

		private static async Task<string> PostSoapAsync(string body)
		{
			using var host = CreateTestHost();
			using var content = new StringContent(body, Encoding.UTF8, "text/xml");
			using var res = await host.CreateRequest("/Service.asmx")
				.AddHeader("SOAPAction", "\"http://tempuri.org/IServiceIssue1161/Process\"")
				.And(msg => msg.Content = content)
				.PostAsync();

			res.EnsureSuccessStatusCode();
			return await res.Content.ReadAsStringAsync();
		}

		private static TestServer CreateTestHost()
		{
			var webHostBuilder = new WebHostBuilder()
				.UseStartup<Startup>()
				.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(typeof(Issue1161Service))));
			return new TestServer(webHostBuilder);
		}
	}
}
