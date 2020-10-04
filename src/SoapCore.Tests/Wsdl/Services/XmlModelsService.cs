using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[XmlType(IncludeInSchema = false)]
	public enum DataChoiceType
	{
		Data4,
		Data5
	}

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
	[ServiceContract(Namespace = "http://bagov.net/")]
	public interface IXmlModelsService
	{
		[OperationContract]
		TestResponseType GetResponse(TestRequestType request);
	}

	public class XmlModelsService : IXmlModelsService
	{
		public TestResponseType GetResponse(TestRequestType request)
		{
			return new TestResponseType();
		}
	}

	[SerializableAttribute]
	[XmlRoot("RequestRoot", Namespace = "http://bagov.net/", IsNullable = false)]
	public class TestRequestType
	{
		[XmlAttributeAttribute]
		public string PropRoot { get; set; }

		[XmlIgnore]
		public string PropIgnore { get; set; }
	}

	[SerializableAttribute]
	[XmlRoot("ResponseRoot", Namespace = "http://bagov.net/", IsNullable = false)]
	public class TestResponseType
	{
		public TestResponseType()
		{
			DataList = new List<TestDataTypeData>();
			DataTypes = new[] { DataChoiceType.Data4, DataChoiceType.Data5 };
		}

		[System.Xml.Serialization.XmlArrayItemAttribute("Data", IsNullable = false)]
		public List<TestDataTypeData> DataList { get; set; }

		[System.Xml.Serialization.XmlArrayItemAttribute("Data2")]
		public List<TestDataTypeData> DataList2 { get; set; }

		[System.Xml.Serialization.XmlArrayItemAttribute("Data")]
		public List<TestDataTypeData> DataList3 { get; set; }

		[System.Xml.Serialization.XmlElementAttribute("Data3")]
		[DataMember]
		public List<TestDataTypeData2> Data { get; set; }

		[XmlIgnore]
		public DataChoiceType[] DataTypes { get; set; }

		[XmlChoiceIdentifier("DataTypes")]
		[XmlElement("Data4", typeof(TestDataTypeData))]
		[XmlElement("Data5", typeof(TestDataTypeData2))]
		public List<object> DataList45 { get; set; }

		[XmlAttributeAttribute]
		public string PropRoot { get; set; }

		[XmlIgnore]
		public string PropIgnore { get; set; }
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
