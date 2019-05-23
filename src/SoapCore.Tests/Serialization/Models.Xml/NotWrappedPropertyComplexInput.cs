using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://tempuri.org/NotWrappedPropertyComplexInput")]
	public class NotWrappedPropertyComplexInput
	{
		[XmlElement(Order = 0)]
		public string StringProperty { get; set; }
	}
}
