using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
	[ServiceContract]
	public interface IXmlModelsService
	{
		[OperationContract]
		TestResponseType GetResponse();
	}

	public class XmlModelsService : IXmlModelsService
	{
		public TestResponseType GetResponse()
		{
			return new TestResponseType();
		}
	}

	[SerializableAttribute]
	[XmlRoot("QueryReportResponse", Namespace = "http://bagov.net/", IsNullable = false)]
	public class TestResponseType
	{
		public TestResponseType()
		{
			DataList = new List<TestDataTypeData>();
		}

		[System.Xml.Serialization.XmlArrayItemAttribute("Data", IsNullable = false)]
		public List<TestDataTypeData> DataList { get; set; }

		[System.Xml.Serialization.XmlArrayItemAttribute("Data", IsNullable = false)]
		public List<TestDataTypeData> DataList2 { get; set; }

		[System.Xml.Serialization.XmlElementAttribute("Data")]
		[DataMember]
		public List<TestDataTypeData2> Data { get; set; }

		[XmlAttributeAttribute]
		public string PropRoot { get; set; }
	}

	[Serializable]
	[XmlType(AnonymousType=true)]
	public class TestDataTypeData
	{
		[XmlAttributeAttribute]
		public string PropAnonymous { get; set; }
	}

	[Serializable]
	[XmlType(AnonymousType=true)]
	[DataContract]
	public class TestDataTypeData2
	{
		[XmlAttributeAttribute]
		[DataMember]
		public string PropAnonymous { get; set; }
	}
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
