using System.ServiceModel;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = "getLinks", WrapperNamespace = "http://xmlelement-namespace/", IsWrapped = true)]
	public class ComplexLegacyModel
	{
		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 0)]
		[XmlElement("unqualified", Form = XmlSchemaForm.Unqualified)]
		public string[] UnqualifiedItems { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 1)]
		[XmlElement("qualified")]
		public string[] QualifiedItems { get; set; }
	}
}
