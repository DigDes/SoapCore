using System;
using System.Diagnostics;

namespace SoapCore.MessageEncoder
{
	[Obsolete]
	internal static class Verify
	{
		[DebuggerStepThrough]
		public static void Operation(bool condition, string message)
		{
			if (!condition)
			{
				throw new InvalidOperationException(message);
			}
		}
	}
}
