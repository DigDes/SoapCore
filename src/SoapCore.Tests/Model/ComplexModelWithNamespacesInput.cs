using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[DataContract(Namespace = "DataModel")]
	public class ComplexModelWithNamespacesInput
	{
		[DataMember]
		public ComplexModelNestedInput ComplexModelNestedInput { get; set; }
	}
}
