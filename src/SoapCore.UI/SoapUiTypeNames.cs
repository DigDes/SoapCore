using System;
using System.Linq;
using System.Text;

namespace SoapCore.UI
{
	/// <summary>
	/// Display names for types that never contain assembly names, versions or public key tokens.
	/// </summary>
	internal static class SoapUiTypeNames
	{
		public static string Format(Type type)
		{
			if (type.IsArray)
			{
				var elementType = type.GetElementType();
				if (elementType != null)
				{
					return Format(elementType) + "[]";
				}
			}

			if (type.IsGenericType)
			{
				var definition = type.GetGenericTypeDefinition();
				var name = RemoveArity(definition.FullName ?? definition.Name);
				return name + "<" + string.Join(", ", type.GetGenericArguments().Select(argument => Format(argument))) + ">";
			}

			return type.FullName ?? type.Name;
		}

		private static string RemoveArity(string name)
		{
			if (name.IndexOf('`') < 0)
			{
				return name;
			}

			// Removes every backtick followed by digits, for example "Outer`1+Inner`2" becomes "Outer+Inner".
			var builder = new StringBuilder(name.Length);
			for (var i = 0; i < name.Length; i++)
			{
				if (name[i] == '`')
				{
					while (i + 1 < name.Length && char.IsDigit(name[i + 1]))
					{
						i++;
					}
				}
				else
				{
					builder.Append(name[i]);
				}
			}

			return builder.ToString();
		}
	}
}
