using System.Diagnostics;
using System.Text;

namespace SoapCore
{
	//original: https://raw.githubusercontent.com/dotnet/corefx/master/src/System.Runtime.Extensions/src/System/Security/SecurityElement.cs
	public class SecurityElement
	{
		private static readonly string[] s_escapeStringPairs = new string[]
	   {
            // these must be all once character escape sequences or a new escaping algorithm is needed
            "<", "&lt;",
			">", "&gt;",
			"\"", "&quot;",
			"\'", "&apos;",
			"&", "&amp;"
	   };

		private static readonly char[] s_escapeChars = new char[] { '<', '>', '\"', '\'', '&' };

		public static string Escape(string str)
		{
			if (str == null)
				return null;

			StringBuilder sb = null;

			int strLen = str.Length;
			int index; // Pointer into the string that indicates the location of the current '&' character
			int newIndex = 0; // Pointer into the string that indicates the start index of the "remaining" string (that still needs to be processed).

			while (true)
			{
				index = str.IndexOfAny(s_escapeChars, newIndex);

				if (index == -1)
				{
					if (sb == null)
						return str;
					else
					{
						sb.Append(str, newIndex, strLen - newIndex);
						return sb.ToString();
					}
				}
				else
				{
					if (sb == null)
						sb = new StringBuilder();

					sb.Append(str, newIndex, index - newIndex);
					sb.Append(GetEscapeSequence(str[index]));

					newIndex = (index + 1);
				}
			}

			// no normal exit is possible
		}

		private static string GetEscapeSequence(char c)
		{
			int iMax = s_escapeStringPairs.Length;
			Debug.Assert(iMax % 2 == 0, "Odd number of strings means the attr/value pairs were not added correctly");

			for (int i = 0; i < iMax; i += 2)
			{
				string strEscSeq = s_escapeStringPairs[i];
				string strEscValue = s_escapeStringPairs[i + 1];

				if (strEscSeq[0] == c)
					return strEscValue;
			}

			Debug.Fail("Unable to find escape sequence for this character");
			return c.ToString();
		}

	}
}
