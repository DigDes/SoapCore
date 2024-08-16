using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[XmlType(AnonymousType = true)]
	public class ComplexTypeAnonymous
	{
		public int IntProperty { get; set; }
		[XmlElement(ElementName = "stringprop")]
		public string StringProperty { get; set; }
		[XmlElement(ElementName = "mybytes")]
		public byte[] ByteArrayProperty { get; set; }

		public Guid MyGuid { get; set; }

		public List<string> StringList { get; set; }

		public List<int> IntList { get; set; }
	}
}
