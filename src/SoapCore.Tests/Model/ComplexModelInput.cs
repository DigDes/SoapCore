using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[DataContract]
	public class ComplexModelInput
	{
		[DataMember]
		public string StringProperty { get; set; }

		[DataMember]
		public int IntProperty { get; set; }

		[DataMember]
		public List<string> ListProperty { get; set; }

		[DataMember]
		public DateTimeOffset DateTimeOffsetProperty { get; set; }
	}
}
