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
		XElement GetXElement(XElement input);

		[OperationContract]
		Date GetDate(Date input);
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

		public Date GetDate(Date input)
		{
			throw new NotImplementedException();
		}
	}
}
