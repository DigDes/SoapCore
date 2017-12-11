using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Models
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
