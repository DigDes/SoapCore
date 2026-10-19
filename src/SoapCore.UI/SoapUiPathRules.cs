namespace SoapCore.UI
{
	/// <summary>
	/// Path validation shared by the MapSoapCoreUi pattern and the EndpointPathPrefix option.
	/// A hand-written ASCII loop on purpose: a regular expression would accept a trailing line feed.
	/// </summary>
	internal static class SoapUiPathRules
	{
		/// <summary>
		/// The longest pattern or prefix accepted.
		/// </summary>
		public const int MaxPathLength = 200;

		public static bool IsValidPath(string? value, int maxLength)
		{
			if (string.IsNullOrEmpty(value) || value.Length > maxLength || value[0] != '/')
			{
				return false;
			}

			var segmentStart = 1;
			for (var i = 1; i < value.Length; i++)
			{
				var c = value[i];
				if (c == '/')
				{
					if (!IsValidSegment(value, segmentStart, i - segmentStart))
					{
						return false;
					}

					segmentStart = i + 1;
				}
				else if (!IsSegmentCharacter(c))
				{
					return false;
				}
			}

			return IsValidSegment(value, segmentStart, value.Length - segmentStart);
		}

		/// <summary>
		/// Checks the StylesheetUrl option: a same-origin path that starts with a single '/', optionally followed by a plain query.
		/// Everything that could point to another origin (a scheme, "//", a backslash) or smuggle something in (whitespace, control characters, a fragment) is refused.
		/// </summary>
		/// <param name="value">The value of the option.</param>
		/// <returns><c>true</c> when the value is a path of the same origin, at most 200 characters.</returns>
		public static bool IsValidStylesheetUrl(string? value)
		{
			// A second '/' right after the first one would be a scheme-relative URL ("//host/x"), which the browser resolves to another origin.
			if (string.IsNullOrEmpty(value) || value.Length < 2 || value.Length > MaxPathLength || value[0] != '/' || value[1] == '/')
			{
				return false;
			}

			var inQuery = false;
			for (var i = 1; i < value.Length; i++)
			{
				var c = value[i];
				if (c == '?' && !inQuery)
				{
					inQuery = true;
				}
				else if (inQuery ? !IsQueryCharacter(c) : !IsUrlPathCharacter(c))
				{
					return false;
				}
			}

			return true;
		}

		private static bool IsUrlPathCharacter(char c)
		{
			return IsSegmentCharacter(c) || c == '%' || c == '/';
		}

		private static bool IsQueryCharacter(char c)
		{
			return IsUrlPathCharacter(c) || c == '=' || c == '&';
		}

		private static bool IsSegmentCharacter(char c)
		{
			return (c >= 'a' && c <= 'z')
				|| (c >= 'A' && c <= 'Z')
				|| (c >= '0' && c <= '9')
				|| c == '.'
				|| c == '_'
				|| c == '~'
				|| c == '-';
		}

		private static bool IsValidSegment(string value, int start, int length)
		{
			if (length == 0)
			{
				return false;
			}

			// The characters were already checked, so only "." and ".." are left to reject.
			if (length == 1 && value[start] == '.')
			{
				return false;
			}

			return !(length == 2 && value[start] == '.' && value[start + 1] == '.');
		}
	}
}
