using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SoapCore.Tests.Model;

namespace SoapCore.Tests
{
	public static class TestServiceKnownTypesProvider
	{
		public static Type[] GetKnownTypes(ICustomAttributeProvider provider)
		{
			return new Type[] { typeof(ComplexModelInput), typeof(ComplexTreeModelInput) };
		}
	}
}
