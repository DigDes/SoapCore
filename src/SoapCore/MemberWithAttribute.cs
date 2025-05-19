using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace SoapCore
{

	public class MemberWithAttribute<TAttribute>(MemberInfo member, TAttribute attribute)
		where TAttribute : Attribute
	{
		public MemberInfo Member { get; private set; } = member;
		public TAttribute Attribute { get; private set; } = attribute;
	}
}
