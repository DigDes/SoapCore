namespace SoapCore.Meta
{
	public class ClrTypeResolver
	{
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
					return "string";
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
