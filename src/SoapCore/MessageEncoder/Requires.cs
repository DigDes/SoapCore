using System;
using System.Diagnostics;

namespace SoapCore.MessageEncoder
{
	internal static class Requires
	{
		[DebuggerStepThrough]
		public static T NotNull<T>(T value, string parameterName)
			where T : class
		{
			if (value is null)
			{
				throw new ArgumentNullException(parameterName);
			}

			return value;
		}

		[DebuggerStepThrough]
		public static void Range(bool condition, string parameterName)
		{
			if (!condition)
			{
				throw new ArgumentOutOfRangeException(parameterName);
			}
		}
	}
}
