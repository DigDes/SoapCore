using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(IsWrapped = false)]
	public class NotWrappedPropertyComplexInputResponse
	{
		[MessageBodyMember(Namespace = "http://tempuri.org/NotWrappedPropertyComplexInput", Order = 0)]
		public NotWrappedPropertyComplexInput NotWrappedComplexInput { get; set; }
	}
}
