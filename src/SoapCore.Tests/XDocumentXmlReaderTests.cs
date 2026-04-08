using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SoapCore.Tests
{
	[TestClass]
	public class XDocumentXmlReaderTests
	{
		[TestMethod]
		public void TestReadBase64()
		{
			var strBase64 = "QkVHSU4KQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhQmxhYmxhYmxhbGJsYUJsYWJsYWJsYWxibGFCbGFibGFibGFsYmxhCkVORA==";

			var xml = @$"
<Request>
	<Base64Data>
{strBase64}
	</Base64Data>
</Request>
";
			var xdoc = XDocument.Parse(xml);
			var reader = new XDocumentXmlReader(xdoc);

			reader.Read();
			reader.Read();

			var buffer = new byte[1024];
			var ms = new MemoryStream();
			while (true)
			{
				var i = reader.ReadElementContentAsBase64(buffer, 0, 1024);
				ms.Write(buffer, 0, i);

				if (i < buffer.Length)
				{
					break;
				}
			}

			ms.Position = 0;

			var b64buffer = Convert.FromBase64String(strBase64);

			Assert.IsTrue(b64buffer.SequenceEqual(ms.ToArray()));

			Assert.AreEqual(1336, ms.Length);
		}

		[TestMethod]
		public async Task WriteTwice()
		{
			var body = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/"">
      <s>1</s>
    </Ping>
  </soapenv:Body>
</soapenv:Envelope>
";
			ParsedMessage pm = await ParsedMessage.FromStreamAsync(new MemoryStream(Encoding.Default.GetBytes(body)), Encoding.Default, MessageVersion.Soap12, CancellationToken.None);

			var dw = XmlDictionaryWriter.CreateDictionaryWriter(XmlWriter.Create(new MemoryStream()));

			pm.WriteBodyContents(dw);
			pm.WriteBodyContents(dw);
		}

		//Test for https://github.com/DigDes/SoapCore/issues/1183
		[TestMethod]
		public async Task TestIssue1183()
		{
			var request =
@$"<?xml version=""1.0"" encoding=""UTF-8""?>
<SOAP-ENV:Envelope
	xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/""
	xmlns:SOAP-ENC=""http://schemas.xmlsoap.org/soap/encoding/""
	xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
	xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
	xmlns:tal-server=""urn:tal_server""
	xmlns:tal-client=""urn:tal_client""
	xmlns:tal=""urn:tal"">
	<SOAP-ENV:Body>
		<tal-server:request>
			<request xsi:type=""tal:CreateSession"">
				<sessionId></sessionId>
				<requestId>2982441</requestId>
				<version>2.5</version>
				<clientUrl>http://192.168.1.2:7000</clientUrl>
				<properties>
					<name>xxx</name>
					<value>false</value>
				</properties>
			</request>
		</tal-server:request>
	</SOAP-ENV:Body>
</SOAP-ENV:Envelope>";

			var expectedBody =
@"<SOAP-ENV:Body xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:SOAP-ENC=""http://schemas.xmlsoap.org/soap/encoding/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:tal-server=""urn:tal_server"" xmlns:tal-client=""urn:tal_client"" xmlns:tal=""urn:tal"">
  <tal-server:request>
    <request xsi:type=""tal:CreateSession"">
      <sessionId></sessionId>
      <requestId>2982441</requestId>
      <version>2.5</version>
      <clientUrl>http://192.168.1.2:7000</clientUrl>
      <properties>
        <name>xxx</name>
        <value>false</value>
      </properties>
    </request>
  </tal-server:request>
</SOAP-ENV:Body>";

			ParsedMessage pm = await ParsedMessage.FromStreamAsync(new MemoryStream(Encoding.Default.GetBytes(request)), Encoding.Default, MessageVersion.Soap11, CancellationToken.None);
			var parsedBody = pm.ToString();
			Assert.IsFalse(pm.IsEmpty);
			Assert.AreEqual(expectedBody, parsedBody);
		}
	}
}
