using System.Runtime.Serialization;

namespace Models
{
    [DataContract]
    public class ComplexReturnModel
    {
        [DataMember]
        public int Id { get; set; }
        [DataMember]
        public string Name { get; set; }
    }
}
