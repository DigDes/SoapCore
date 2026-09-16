using System;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Issue1194;

[Serializable]
[XmlType(Namespace = "urn:test-namespace.xms")]
[XmlRoot(Namespace = "urn:test-namespace.xms", IsNullable = false)]
public class TestHeader
{
	private string _toString;
	private string _fromString;

	/// <remarks />
	public string From
	{
		get { return _fromString; }
		set { _fromString = value; }
	}

	/// <remarks />
	public string To
	{
		get { return _toString; }
		set { _toString = value; }
	}
}

[ServiceContract(Namespace = "urn:test-namespace-portType")]
public interface ITestService
{
	[OperationContract(Action = "urn:test-namespace-binding:Test", ReplyAction = "*")]
	[XmlSerializerFormat]
	TestRequestOut Test(TestRequestIn request);
}

[MessageContract(IsWrapped = false)]
public class TestRequestIn
{
	[MessageHeader(Namespace = "urn:test-namespace.xms")]
	public TestHeader TestHeader { get; set; }

	[MessageBodyMember(Namespace = "urn:test-namespace.test", Order = 0)]
	public TestRequestType TestRequest { get; set; }
}

[Serializable]
[XmlType(Namespace = "urn:test-namespace.test")]
public class TestRequestType
{
	public string TestIn { get; set; }
}

[MessageContract(IsWrapped = false)]
public class TestRequestOut
{
	[MessageHeader(Namespace = "urn:test-namespace.xms")]
	public TestHeader TestHeader { get; set; }

	[MessageBodyMember(Namespace = "urn:test-namespace.test", Order = 0)]
	public TestResponseType TestResult { get; set; }
}

[Serializable]
[XmlType(Namespace = "urn:test-namespace.test")]
public class TestResponseType
{
	public string TestResult { get; set; }
}
