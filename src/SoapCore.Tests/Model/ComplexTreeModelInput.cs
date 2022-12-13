using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace SoapCore.Tests.Model
{
	[DataContract]
	public class ComplexTreeModelInput : IComplexTreeModelInput
	{
		[DataMember]
		public ComplexModelInput Item { get; set; }
	}
}
