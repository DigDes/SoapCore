namespace SoapCore.UI
{
	/// <summary>
	/// Whether the request and the response of an operation are message contracts, in the JSON returned by the services API.
	/// Only filled in when <see cref="SoapUiOptions.ExposeOperationDetails"/> is enabled.
	/// </summary>
	internal sealed class SoapUiMessageContractDto
	{
		public bool Request { get; init; }

		public bool Response { get; init; }
	}
}
