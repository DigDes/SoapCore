using System;
using System.Diagnostics;

namespace SoapCore.MessageEncoder
{
	[Obsolete]
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
	}
}
