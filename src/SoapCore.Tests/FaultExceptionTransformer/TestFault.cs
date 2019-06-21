using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace SoapCore.Tests.FaultExceptionTransformer
{
	[DataContract]
	public class TestFault
	{
		public TestFault()
		{
		}

		[DataMember]
		public string Message { get; set; }

		[DataMember]
		public string AdditionalProperty { get; set; }
	}
}
