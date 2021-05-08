using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[DataContract]
	public class TypeWithNonDataMembers
	{
		[DataMember]
		public string StringMember { get; set; }

		public string StringNonMemberNonSerializable { get; set; }

		[DataMember]
		public List<string> StringListMember { get; set; }

		public List<string> StringListNonMemberNonSerializable { get; set; }

		[DataMember]
		public List<TypeWithNonDataMembersInner> TypeWithNonDataMembersInnerListMember { get; set; }

		public List<TypeWithNonDataMembersInner> TypeWithNonDataMembersInnerListNonMemberNonSerializable { get; set; }
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Tightly coupled")]
	[DataContract]
	public class TypeWithNonDataMembersInner
	{
		[DataMember]
		public int IntMemberInner { get; set; }

		public int IntNonMemberInnerNonSerializable { get; set; }

		[DataMember]
		public List<int> IntListMemberInner { get; set; }

		public List<int> IntListNonMemberInnerNonSerializable { get; set; }
	}
}
