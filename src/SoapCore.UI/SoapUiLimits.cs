namespace SoapCore.UI
{
	/// <summary>
	/// The defaults and the valid ranges of the numeric options of the explorer, shared by the options, their validator and the catalog.
	/// </summary>
	internal static class SoapUiLimits
	{
		/// <summary>
		/// The default (and the maximum the user can choose) timeout, in seconds, of one request sent from the explorer.
		/// </summary>
		public const int DefaultSendTimeoutSeconds = 60;

		/// <summary>
		/// The smallest valid <see cref="SoapUiOptions.SendTimeoutSeconds"/>.
		/// </summary>
		public const int MinSendTimeoutSeconds = 1;

		/// <summary>
		/// The largest valid <see cref="SoapUiOptions.SendTimeoutSeconds"/>.
		/// </summary>
		public const int MaxSendTimeoutSeconds = 600;

		/// <summary>
		/// The default <see cref="SoapUiOptions.MaxResponseBytes"/>: 5 MiB.
		/// </summary>
		public const int DefaultMaxResponseBytes = 5242880;

		/// <summary>
		/// The smallest valid <see cref="SoapUiOptions.MaxResponseBytes"/>: 1 KiB.
		/// </summary>
		public const int MinResponseBytes = 1024;

		/// <summary>
		/// The largest valid <see cref="SoapUiOptions.MaxResponseBytes"/>: 50 MiB.
		/// </summary>
		public const int MaxResponseBytes = 52428800;

		public static bool IsValidSendTimeoutSeconds(int seconds)
		{
			return seconds >= MinSendTimeoutSeconds && seconds <= MaxSendTimeoutSeconds;
		}

		public static bool IsValidMaxResponseBytes(int bytes)
		{
			return bytes >= MinResponseBytes && bytes <= MaxResponseBytes;
		}
	}
}
