using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace SoapCore.Tests
{
	[DataContract]
	public class ComplexModelInput
	{
		[DataMember]
		public string StringProperty { get; set; }

		[DataMember]
		public int IntProperty { get; set; }
	}
}
