using System;
using System.Data;
using System.ServiceModel;
using System.Xml.Linq;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IXmlSchemaProviderTypeService
	{
		[OperationContract]
		DataTable GetDataTable(DataTable input);

		[OperationContract]
		System.Xml.Linq.XElement GetXElement(System.Xml.Linq.XElement input);
	}

	public class XmlSchemaProviderTypeService : IXmlSchemaProviderTypeService
	{
		public DataTable GetDataTable(DataTable input)
		{
			throw new NotImplementedException();
		}

		public XElement GetXElement(XElement input)
		{
			throw new NotImplementedException();
		}
	}
}
