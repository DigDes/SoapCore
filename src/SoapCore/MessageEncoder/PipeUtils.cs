using System.IO.Pipelines;

namespace SoapCore.MessageEncoder
{
	internal static class PipeUtils
	{
		internal static void ScrubAfterAdvanceTo(this ref ReadResult readResult) => readResult = new ReadResult(default, readResult.IsCanceled, readResult.IsCompleted);
	}
}
