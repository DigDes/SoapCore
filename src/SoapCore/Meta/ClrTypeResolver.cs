using System;

namespace SoapCore.Meta
{
	public class ClrTypeResolver
	{
		/// <summary>
		/// When true, Guid types are resolved as "guid" (for Microsoft WSDL types namespace)
		/// instead of "string". This affects array naming (List&lt;Guid&gt; becomes ArrayOfGuid).
		/// </summary>
		[ThreadStatic]
		public static bool UseMicrosoftGuid;

		public static string ResolveOrDefault(string typeName)
		{
			switch (typeName)
			{
				case "Boolean":
					return "boolean";
				case "Byte":
					return "unsignedByte";
				case "Int16":
					return "short";
				case "Int32":
					return "int";
				case "Int64":
					return "long";
				case "SByte":
					return "byte";
				case "UInt16":
					return "unsignedShort";
				case "UInt32":
					return "unsignedInt";
				case "UInt64":
					return "unsignedLong";
				case "Decimal":
					return "decimal";
				case "Double":
					return "double";
				case "Single":
					return "float";
				case "DateTime":
					return "dateTime";
				case "Guid":
					// When UseMicrosoftGuid is true, return null so callers fall back to original type name "Guid"
					// This ensures ArrayOfGuid naming while avoiding invalid "s:guid" type references
					return UseMicrosoftGuid ? null : "string";
				case "Char":
					return "string";
				case "TimeSpan":
					return "duration";
				case "String":
					return "string";
				case "Byte[]":
					return "base64Binary";
#if NET6_0_OR_GREATER
				case "DateOnly":
					return "date";
#endif
			}

			return null;
		}
	}
}
