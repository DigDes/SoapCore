using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SoapCore.Tests.Model
{
	[KnownType(nameof(GetKnownTypes))]
	[DataContract(Name = "ComplexInheritanceModelInputBase")]
	public abstract class ComplexInheritanceModelInputBase
	{
		[DataMember]
		public string StringProperty { get; set; }

		[DataMember]
		public abstract string Example { get; set; }

		private static IEnumerable<Type> GetKnownTypes()
		{
			yield return typeof(ComplexInheritanceModelInputA);
			yield return typeof(ComplexInheritanceModelInputB);
		}
	}
}
