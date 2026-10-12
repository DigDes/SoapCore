using System;
using System.Reflection;
using System.Threading;

namespace SoapCore.UI
{
	/// <summary>
	/// The version of the SoapCore.UI assembly, as shown by the explorer. It is the informational version without the build metadata
	/// (everything from the first '+', which is where a commit hash would be), or the assembly version when that is missing or not plain.
	/// Only letters, digits, '.', '-' and '_' are ever returned: never a path, a machine name or any other build-time data.
	/// </summary>
	internal static class SoapUiVersion
	{
		private const int MaxLength = 64;
		private const string UnknownVersion = "0.0.0";

		private static readonly Lazy<string> Value = new Lazy<string>(Read, LazyThreadSafetyMode.ExecutionAndPublication);

		public static string Current => Value.Value;

		private static string Read()
		{
			var assembly = typeof(SoapUiVersion).Assembly;

			var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			var cleaned = Clean(informational);
			if (cleaned != null)
			{
				return cleaned;
			}

			cleaned = Clean(assembly.GetName().Version?.ToString());
			return cleaned ?? UnknownVersion;
		}

		private static string? Clean(string? text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}

			var plus = text.IndexOf('+');
			var core = (plus >= 0 ? text.Substring(0, plus) : text).Trim();
			if (core.Length == 0 || core.Length > MaxLength)
			{
				return null;
			}

			foreach (var c in core)
			{
				if (!IsVersionCharacter(c))
				{
					return null;
				}
			}

			return core;
		}

		private static bool IsVersionCharacter(char c)
		{
			return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == '-' || c == '_';
		}
	}
}
