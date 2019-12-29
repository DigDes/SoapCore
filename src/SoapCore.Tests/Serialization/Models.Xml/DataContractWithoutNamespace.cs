using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[DataContract]
	public class DataContractWithoutNamespace
	{
		[DataMember]
		public int IntProperty { get; set; }

		[DataMember]
		public string StringProperty { get; set; }
	}
}
