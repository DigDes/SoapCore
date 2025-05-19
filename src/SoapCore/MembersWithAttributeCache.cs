using System;
using System.Collections.Concurrent;

namespace SoapCore
{
	internal static partial class ReflectionExtensions
	{
		private static class MembersWithAttributeCache<TAttribute> where TAttribute : Attribute
		{
			public static ConcurrentDictionary<Type, MemberWithAttribute<TAttribute>[]> CacheEntries = new();
		}
	}
}
