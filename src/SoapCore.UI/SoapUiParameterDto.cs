namespace SoapCore.UI
{
	/// <summary>
	/// One parameter of an operation in the JSON returned by the services API. The declaration order is the JSON order.
	/// Only filled in when <see cref="SoapUiOptions.ExposeOperationDetails"/> is enabled.
	/// <c>Type</c> is a display name without assembly information (for out and ref parameters, the type behind the reference)
	/// and <c>Direction</c> is "in", "out" or "ref". <c>Description</c> is plain text from <c>[Description]</c> or the <c>param</c> of the XML documentation, or <c>null</c>.
	/// </summary>
	internal sealed class SoapUiParameterDto
	{
		public string Name { get; init; } = string.Empty;

		public string Type { get; init; } = string.Empty;

		public string Direction { get; init; } = "in";

		public string? Description { get; init; }
	}
}
