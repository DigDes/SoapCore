namespace SoapCore.UI
{
	/// <summary>
	/// The return value of an operation in the JSON returned by the services API.
	/// Only filled in when <see cref="SoapUiOptions.ExposeOperationDetails"/> is enabled.
	/// <c>Type</c> is a display name without assembly information, or "void" when the operation returns nothing.
	/// </summary>
	internal sealed class SoapUiReturnDto
	{
		public string Type { get; init; } = "void";
	}
}
