using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(IsWrapped = false)]
	public class NotWrappedFieldDoubleComplexInputRequest
	{
#pragma warning disable SA1401 // Fields must be private
		[MessageBodyMember(Namespace = "http://tempuri.org/NotWrappedFieldDoubleComplexInput", Order = 0)]
		public NotWrappedFieldComplexInput NotWrappedComplexInput1;

		[MessageBodyMember(Namespace = "http://tempuri.org/NotWrappedFieldDoubleComplexInput", Order = 1)]
		public NotWrappedFieldComplexInput NotWrappedComplexInput2;
#pragma warning restore SA1401 // Fields must be private
	}
}
