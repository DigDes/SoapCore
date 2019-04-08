using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Models
{
	[DataContract]
	public class ComplexModelResponse
	{
		public float FloatProperty { get; set; }
		public string StringProperty { get; set; }
		public List<string> ListProperty { get; set; }

		public DateTimeOffset DateTimeOffsetProperty { get; set; }

		[DataMember]
		public TestEnum TestEnum { get; set; }
	}

	public enum TestEnum
	{
		One,
		Two
	}
}
