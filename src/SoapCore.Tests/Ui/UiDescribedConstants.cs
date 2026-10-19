namespace SoapCore.Tests.Ui
{
	// Constants for the [Description] attributes of the described fixtures (an attribute argument must be a constant).
	public static class UiDescribedConstants
	{
		// 600 characters: longer than the 500 characters the explorer serves.
		public const string Long = Chunk + Chunk + Chunk + Chunk + Chunk + Chunk;

		private const string Chunk = "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789";
	}
}
