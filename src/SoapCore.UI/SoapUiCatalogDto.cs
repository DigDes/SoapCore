using System;
using System.Collections.Generic;

namespace SoapCore.UI
{
	/// <summary>
	/// Root of the JSON returned by the services API. Serialized with camelCase names. The declaration order is the JSON order.
	/// <c>Description</c>, <c>DefaultSoapVersion</c> and <c>StylesheetUrl</c> are <c>null</c> when the option is not set (the JSON keeps the keys).
	/// <c>DocExpansion</c> is "list" or "full". <c>AuthSchemes</c> lists "bearer", "basic", "apiKey" and "wsUsernameToken" (in that order), or nothing when the panel is hidden.
	/// <c>FormEditorEnabled</c>, <c>SendTimeoutSeconds</c> and <c>MaxResponseBytes</c> are the options of the same name, and <c>UiVersion</c> is the version of
	/// SoapCore.UI without build metadata (see <see cref="SoapUiVersion"/>). They sit right after <c>StylesheetUrl</c>, so <c>Services</c> stays the last member.
	/// </summary>
	internal sealed class SoapUiCatalogDto
	{
		public string Title { get; init; } = string.Empty;

		public string? Description { get; init; }

		public bool TryItOutEnabled { get; init; }

		public bool OperationDetailsEnabled { get; init; }

		public string BasePath { get; init; } = string.Empty;

		public string? DefaultSoapVersion { get; init; }

		public string DocExpansion { get; init; } = "list";

		public IReadOnlyList<string> AuthSchemes { get; init; } = Array.Empty<string>();

		public string ApiKeyHeaderName { get; init; } = string.Empty;

		public string? StylesheetUrl { get; init; }

		public bool FormEditorEnabled { get; init; }

		public int SendTimeoutSeconds { get; init; }

		public int MaxResponseBytes { get; init; }

		public string UiVersion { get; init; } = string.Empty;

		public IReadOnlyList<SoapUiServiceDto> Services { get; init; } = Array.Empty<SoapUiServiceDto>();
	}
}
