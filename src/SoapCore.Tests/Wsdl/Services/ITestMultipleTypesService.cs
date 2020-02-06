using System;
using System.Data;
using System.ServiceModel;
using System.Xml.Linq;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface ITestMultipleTypesService
	{
		[OperationContract]
		DataTable GetDataTable(DataTable input);

		[OperationContract]
		System.Xml.Linq.XElement GetXElement(System.Xml.Linq.XElement input);

		[OperationContract]
		DateTimeOffset GetDateTimeOffset(DateTimeOffset input);

		[OperationContract]
		int GetInt(int input);

		[OperationContract]
		byte[] GetBytes(byte[] input);

		[OperationContract]
		string GetString(string input);

		[OperationContract]
		MyClass GetMyClass(MyClass input);
	}

	public class TestMultipleTypesService : ITestMultipleTypesService
	{
		public DataTable GetDataTable(DataTable input)
		{
			throw new NotImplementedException();
		}

		public DateTimeOffset GetDateTimeOffset(DateTimeOffset input)
		{
			throw new NotImplementedException();
		}

		public int GetInt(int input)
		{
			throw new NotImplementedException();
		}

		public string GetString(string input)
		{
			throw new NotImplementedException();
		}

		public XElement GetXElement(XElement input)
		{
			throw new NotImplementedException();
		}

		public MyClass GetMyClass(MyClass input)
		{
			return input;
		}

		public byte[] GetBytes(byte[] input)
		{
			throw new NotImplementedException();
		}
	}
}
