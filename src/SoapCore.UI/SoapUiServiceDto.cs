using System;
using System.Collections.Generic;

namespace SoapCore.UI
{
	/// <summary>
	/// One SOAP endpoint in the JSON returned by the services API. The declaration order is the JSON order.
	/// <c>Description</c> is plain text from <c>[Description]</c> or the XML documentation of the service, or <c>null</c> (the JSON keeps the key).
	/// </summary>
	internal sealed class SoapUiServiceDto
	{
		public string Id { get; init; } = string.Empty;

		public string Name { get; init; } = string.Empty;

		public string Path { get; init; } = string.Empty;

		public string WsdlUrl { get; init; } = string.Empty;

		public string ServiceType { get; init; } = string.Empty;

		public string Serializer { get; init; } = string.Empty;

		public string? CustomSerializerType { get; init; }

		public IReadOnlyList<string> SoapVersions { get; init; } = Array.Empty<string>();

		public bool CaseInsensitivePath { get; init; }

		public string? Description { get; init; }

		public IReadOnlyList<SoapUiOperationDto> Operations { get; init; } = Array.Empty<SoapUiOperationDto>();

		public string? OperationsError { get; init; }
	}
}
