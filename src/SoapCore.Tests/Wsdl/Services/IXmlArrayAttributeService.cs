using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services;

[ServiceContract]
public interface IXmlArrayAttributeService
{
	[OperationContract]
	TypeWithXmlArrayAttribute Method(TypeWithXmlArrayAttribute argument);
}

public class XmlArrayAttributeService : IXmlArrayAttributeService
{
	public TypeWithXmlArrayAttribute Method(TypeWithXmlArrayAttribute argument)
	{
		return new TypeWithXmlArrayAttribute();
	}
}

public class TypeWithXmlArrayAttribute
{
	[XmlArray("AvlRoomTypeItems")]
	public List<AvlRoomTypeItem> AvlRoomTypeList { get; set; }
}

public class AvlRoomTypeItem
{
	public string RoomTypeCode { get; set; }
}
