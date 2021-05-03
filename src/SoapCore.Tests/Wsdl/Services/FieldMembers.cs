using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "Required for test")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Required for test")]
	[DataContract]
	public class FieldMembers
	{
		[DataMember]
		public string StringPropMember { get; set; }

		[DataMember]
		public string StringFieldMember;

		[DataMember]
		public List<string> StringListFieldMember;

		[DataMember]
		public List<string> StringListPropMember { get; set; }

		[DataMember]
		public List<TestDataType> TestDataTypeListFieldMember;

		[DataMember]
		public List<TestDataType> TestDataTypeListPropMember { get; set; }
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "Required for test")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Required for test")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Tightly coupled")]
	[DataContract]
	public class TestDataType
	{
		[DataMember]
		public List<string> TestDataTypeStringListFieldMember;

		[DataMember]
		public List<string> TestDataTypeStringListPropMember { get; set; }

		[DataMember]
		public string TestDataTypeStringPropMember { get; set; }

		[DataMember]
		public string TestDataTypeStringFieldMember;
	}
}
