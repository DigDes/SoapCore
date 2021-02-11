using System.ServiceModel;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = "getLinks", WrapperNamespace = "http://xmlelement-namespace/", IsWrapped = true)]
	public class ComplexLegacyModel
	{
		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 0)]
		[XmlElement("qualified", Form = XmlSchemaForm.Unqualified)]
		public string[] QualifiedItems { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 0)]
		[XmlElement("unqualified")]
		public string[] UnqualifiedItems { get; set; }
	}
}
