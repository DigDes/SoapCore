using System;
using System.Diagnostics;

namespace SoapCore.MessageEncoder
{
	internal static class Verify
	{
		[DebuggerStepThrough]
		public static void NotDisposed(PipeStream disposedValue)
		{
			Requires.NotNull(disposedValue, nameof(disposedValue));

			if (disposedValue.IsDisposed)
			{
				string objectName = disposedValue.GetType().FullName;
				throw new ObjectDisposedException(objectName);
			}
		}

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
