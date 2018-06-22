using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Models
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

        [DataMember]
        public List<ComplexObject> ComplexListProperty { get; set; }
    }

    [DataContract]
    public class ComplexObject
    {
        [DataMember]
        public string StringProperty { get; set; }

        [DataMember]
        public int IntProperty { get; set; }
    }
}
