using System;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[Serializable]
	public class ArrayOfStringModel
	{
		[XmlElement("file")]
		public string[] File
		{
			get;
			set;
		}
	}
}
