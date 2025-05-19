using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

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

        [DataMember]
        public List<BaseObject> DerivedObjects { get; set; }

        [DataContract]
        [ServiceKnownType(typeof(DerivedObject))]
        public class DerivedObject : BaseObject
        {
            public DerivedObject()
            {
                Name = nameof(DerivedObject); 
            }
        }
    }

    [DataContract]
    public class ComplexObject
    {
        [DataMember]
        public string StringProperty { get; set; }

        [DataMember]
        public int IntProperty { get; set; }
    }

    [DataContract]
    public class BaseObject
    {
        public string Name { get; set; } = nameof(BaseObject);
    }
}
