using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace SoapCore.UI
{
	/// <summary>
	/// Validates <see cref="SoapUiOptions"/> and reports every problem at once.
	/// </summary>
	internal sealed class SoapUiOptionsValidator : IValidateOptions<SoapUiOptions>
	{
		private const int MaxTitleLength = 100;
		private const int MaxDescriptionLength = 300;

		public ValidateOptionsResult Validate(string? name, SoapUiOptions options)
		{
			var errors = new List<string>();

			var title = options.Title;
			if (string.IsNullOrWhiteSpace(title))
			{
				errors.Add("Title must not be empty.");
			}

			if (title != null)
			{
				if (title.Length > MaxTitleLength)
				{
					errors.Add("Title must be at most " + MaxTitleLength.ToString(CultureInfo.InvariantCulture) + " characters.");
				}

				if (ContainsControlCharacter(title))
				{
					errors.Add("Title must not contain control characters.");
				}
			}

			var prefix = options.EndpointPathPrefix;
			if (!string.IsNullOrEmpty(prefix) && !SoapUiPathRules.IsValidPath(prefix, SoapUiPathRules.MaxPathLength))
			{
				errors.Add("EndpointPathPrefix must be empty or a path like '/soap' (letters, digits, '.', '_', '~', '-'; no trailing slash).");
			}

			ValidateDescription(options.Description, errors);
			ValidateDefaultSoapVersion(options.DefaultSoapVersion, errors);

			if (!Enum.IsDefined(options.DocExpansion))
			{
				errors.Add("DocExpansion must be SoapUiDocExpansion.List or SoapUiDocExpansion.Full.");
			}

			if (((int)options.AuthSchemes & ~(int)SoapUiAuthSchemes.All) != 0)
			{
				errors.Add("AuthSchemes must only combine SoapUiAuthSchemes.Bearer, Basic, ApiKey and WsUsernameToken (or be None or All).");
			}

			ValidateApiKeyHeaderName(options.ApiKeyHeaderName, errors);

			var stylesheetUrl = options.StylesheetUrl;
			if (!string.IsNullOrEmpty(stylesheetUrl) && !SoapUiPathRules.IsValidStylesheetUrl(stylesheetUrl))
			{
				errors.Add("StylesheetUrl must be empty or a path of the same origin that starts with a single '/', like '/branding.css' (letters, digits, '.', '_', '~', '-', '%', '/', and an optional query with '=' and '&'; at most "
					+ SoapUiPathRules.MaxPathLength.ToString(CultureInfo.InvariantCulture) + " characters). Absolute and scheme-relative URLs are not allowed.");
			}

			if (!SoapUiLimits.IsValidSendTimeoutSeconds(options.SendTimeoutSeconds))
			{
				errors.Add("SendTimeoutSeconds must be between " + SoapUiLimits.MinSendTimeoutSeconds.ToString(CultureInfo.InvariantCulture)
					+ " and " + SoapUiLimits.MaxSendTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " seconds.");
			}

			if (!SoapUiLimits.IsValidMaxResponseBytes(options.MaxResponseBytes))
			{
				errors.Add("MaxResponseBytes must be between " + SoapUiLimits.MinResponseBytes.ToString(CultureInfo.InvariantCulture)
					+ " and " + SoapUiLimits.MaxResponseBytes.ToString(CultureInfo.InvariantCulture) + " bytes (1 KiB to 50 MiB).");
			}

			return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
		}

		private static void ValidateDescription(string? description, List<string> errors)
		{
			if (description == null)
			{
				return;
			}

			if (description.Length > MaxDescriptionLength)
			{
				errors.Add("Description must be at most " + MaxDescriptionLength.ToString(CultureInfo.InvariantCulture) + " characters.");
			}

			if (ContainsControlCharacter(description))
			{
				errors.Add("Description must not contain control characters.");
			}
		}

		private static void ValidateDefaultSoapVersion(string? version, List<string> errors)
		{
			if (string.IsNullOrEmpty(version))
			{
				return;
			}

			if (!string.Equals(version, "1.1", StringComparison.Ordinal) && !string.Equals(version, "1.2", StringComparison.Ordinal))
			{
				errors.Add("DefaultSoapVersion must be null (automatic), \"1.1\" or \"1.2\".");
			}
		}

		private static void ValidateApiKeyHeaderName(string? headerName, List<string> errors)
		{
			if (string.IsNullOrEmpty(headerName))
			{
				errors.Add("ApiKeyHeaderName must not be empty.");
				return;
			}

			if (!SoapUiHeaderRules.IsValidName(headerName))
			{
				errors.Add("ApiKeyHeaderName must be 1 to " + SoapUiHeaderRules.MaxNameLength.ToString(CultureInfo.InvariantCulture) + " characters of letters, digits, '-' or '_'.");
			}
			else if (SoapUiHeaderRules.IsOwnedByExplorer(headerName))
			{
				errors.Add("ApiKeyHeaderName cannot be '" + headerName + "': the explorer sets that header itself.");
			}
			else if (SoapUiHeaderRules.IsForbiddenInBrowsers(headerName))
			{
				errors.Add("ApiKeyHeaderName cannot be '" + headerName + "': browsers do not let a page set that header.");
			}
		}

		private static bool ContainsControlCharacter(string value)
		{
			foreach (var c in value)
			{
				if (char.IsControl(c))
				{
					return true;
				}
			}

			return false;
		}
	}
}
