using System.Collections.Generic;

namespace SoapCore.UI
{
	/// <summary>
	/// One operation of a SOAP endpoint in the JSON returned by the services API. The declaration order is the JSON order.
	/// <c>Parameters</c>, <c>Returns</c>, <c>Faults</c> and <c>MessageContract</c> are <c>null</c> unless
	/// <see cref="SoapUiOptions.ExposeOperationDetails"/> is enabled (the JSON keeps the keys, with <c>null</c> values).
	/// <c>Description</c> is plain text from <c>[Description]</c> or the XML documentation of the operation, or <c>null</c>. It is not a detail:
	/// it is served whatever <see cref="SoapUiOptions.ExposeOperationDetails"/> says.
	/// </summary>
	internal sealed class SoapUiOperationDto
	{
		public string Name { get; init; } = string.Empty;

		public string SoapAction { get; init; } = string.Empty;

		public bool IsOneWay { get; init; }

		public string Contract { get; init; } = string.Empty;

		public string ContractNamespace { get; init; } = string.Empty;

		public string? Description { get; init; }

		public IReadOnlyList<SoapUiParameterDto>? Parameters { get; init; }

		public SoapUiReturnDto? Returns { get; init; }

		public IReadOnlyList<string>? Faults { get; init; }

		public SoapUiMessageContractDto? MessageContract { get; init; }
	}
}
