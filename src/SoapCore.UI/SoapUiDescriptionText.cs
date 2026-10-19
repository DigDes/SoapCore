using System;
using System.Globalization;
using System.Text;

namespace SoapCore.UI
{
	/// <summary>
	/// Turns the text of a description into what the explorer API serves: plain text, one line, at most <see cref="MaxLength"/> characters.
	/// Nothing here produces markup. The explorer writes the text with <c>textContent</c>.
	/// </summary>
	internal static class SoapUiDescriptionText
	{
		/// <summary>
		/// The longest description served. A longer text is cut and ends with "...".
		/// </summary>
		public const int MaxLength = 500;

		private const string Ellipsis = "...";

		/// <summary>
		/// Cleans a description: whitespace runs (including line breaks) become one space, control and format characters and
		/// unpaired surrogates are dropped, the text is trimmed and capped.
		/// </summary>
		/// <param name="text">The raw text.</param>
		/// <returns>The plain text, or <c>null</c> when nothing is left.</returns>
		public static string? Normalize(string? text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}

			// One character more than the cap is enough to know that the text is too long.
			var limit = MaxLength + 1;
			var builder = new StringBuilder(Math.Min(text.Length, limit));
			var pendingSpace = false;

			for (var i = 0; i < text.Length && builder.Length < limit; i++)
			{
				var c = text[i];
				if (char.IsWhiteSpace(c))
				{
					pendingSpace = true;
					continue;
				}

				if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
				{
					AppendSeparator(builder, ref pendingSpace);
					builder.Append(c).Append(text[i + 1]);
					i++;
				}
				else if (!char.IsSurrogate(c) && !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format)
				{
					AppendSeparator(builder, ref pendingSpace);
					builder.Append(c);
				}
			}

			if (builder.Length == 0)
			{
				return null;
			}

			if (builder.Length <= MaxLength)
			{
				return builder.ToString();
			}

			var length = MaxLength - Ellipsis.Length;
			if (char.IsHighSurrogate(builder[length - 1]))
			{
				length--;
			}

			return builder.ToString(0, length).TrimEnd() + Ellipsis;
		}

		/// <summary>
		/// Reduces a <c>cref</c> attribute value of the XML documentation to its last segment:
		/// "T:System.String" becomes "String", "M:Ns.Type.Method(System.Int32)" becomes "Method", "List{T}" becomes "List".
		/// </summary>
		/// <param name="cref">The value of the <c>cref</c> attribute.</param>
		/// <returns>The name, which can be empty.</returns>
		public static string CrefName(string? cref)
		{
			if (string.IsNullOrEmpty(cref))
			{
				return string.Empty;
			}

			var start = cref.Length > 1 && cref[1] == ':' ? 2 : 0;
			var builder = new StringBuilder(cref.Length);
			var depth = 0;
			for (var i = start; i < cref.Length; i++)
			{
				var c = cref[i];
				if (c == '(' && depth == 0)
				{
					break;
				}

				if (c == '{')
				{
					depth++;
				}
				else if (c == '}')
				{
					if (depth > 0)
					{
						depth--;
					}
				}
				else if (depth == 0)
				{
					builder.Append(c);
				}
			}

			var name = builder.ToString();
			var lastDot = name.LastIndexOf('.');
			if (lastDot >= 0)
			{
				name = name.Substring(lastDot + 1);
			}

			// "List`1" is "List".
			var tick = name.IndexOf('`');
			return tick >= 0 ? name.Substring(0, tick) : name;
		}

		private static void AppendSeparator(StringBuilder builder, ref bool pendingSpace)
		{
			if (pendingSpace && builder.Length > 0)
			{
				builder.Append(' ');
			}

			pendingSpace = false;
		}
	}
}
