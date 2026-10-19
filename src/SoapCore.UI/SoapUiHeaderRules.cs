using System;
using System.Collections.Generic;

namespace SoapCore.UI
{
	/// <summary>
	/// The rules for a request header name that the explorer is asked to send (the ApiKeyHeaderName option).
	/// They mirror the header validation of the explorer script (wsdl-parser.js: FORBIDDEN_HEADERS, FORBIDDEN_HEADER_PREFIXES and OWNED_HEADERS),
	/// so a name accepted here is a name the browser can really send.
	/// </summary>
	internal static class SoapUiHeaderRules
	{
		/// <summary>
		/// The longest header name accepted.
		/// </summary>
		public const int MaxNameLength = 128;

		// Headers the explorer sets itself.
		private static readonly HashSet<string> OwnedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"content-type", "soapaction",
		};

		// Headers a page is not allowed to set (the "forbidden request header" list of the Fetch standard, as far as the explorer script knows it).
		private static readonly HashSet<string> ForbiddenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"host", "content-length", "connection", "cookie", "cookie2", "origin", "referer", "date", "expect",
			"keep-alive", "te", "trailer", "transfer-encoding", "upgrade", "via", "dnt", "accept-charset", "accept-encoding",
		};

		private static readonly string[] ForbiddenPrefixes = { "proxy-", "sec-", "access-control-request-" };

		/// <summary>
		/// Gets a value indicating whether the name has only the characters of a plain header name and a sensible length.
		/// A hand-written ASCII loop on purpose: a regular expression would accept a trailing line feed.
		/// </summary>
		/// <param name="name">The header name.</param>
		/// <returns><c>true</c> when the name is 1 to 128 letters, digits, '-' or '_'.</returns>
		public static bool IsValidName(string? name)
		{
			if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
			{
				return false;
			}

			foreach (var c in name)
			{
				var valid = (c >= 'a' && c <= 'z')
					|| (c >= 'A' && c <= 'Z')
					|| (c >= '0' && c <= '9')
					|| c == '-'
					|| c == '_';
				if (!valid)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Gets a value indicating whether the explorer sets this header itself (Content-Type and SOAPAction).
		/// </summary>
		/// <param name="name">The header name.</param>
		/// <returns><c>true</c> when the header belongs to the explorer.</returns>
		public static bool IsOwnedByExplorer(string name)
		{
			return OwnedHeaders.Contains(name);
		}

		/// <summary>
		/// Gets a value indicating whether browsers refuse to let a page set this header.
		/// </summary>
		/// <param name="name">The header name.</param>
		/// <returns><c>true</c> when the header is forbidden.</returns>
		public static bool IsForbiddenInBrowsers(string name)
		{
			if (ForbiddenHeaders.Contains(name))
			{
				return true;
			}

			foreach (var prefix in ForbiddenPrefixes)
			{
				if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}
