using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SoapCore.UI
{
	/// <summary>
	/// Builds the member ids of the XML documentation (the <c>name</c> of each <c>member</c> element), following the format the C# compiler writes:
	/// <c>T:Namespace.Type</c> for types and <c>M:Namespace.Type.Method(System.String,System.Int32)</c> for methods.
	/// Nested types are separated with '.', generic types and methods carry their arity (<c>`1</c>, <c>``1</c>), the type parameters used in a
	/// parameter list are written <c>`0</c> (of the type) and <c>``0</c> (of the method), constructed types use braces
	/// (<c>System.Collections.Generic.List{System.String}</c>), arrays <c>[]</c> (<c>[0:,0:]</c> for more dimensions), by-reference parameters '@' and pointers '*'.
	/// Properties, fields, events, constructors and explicit interface implementations are not covered: the explorer does not need them.
	/// </summary>
	internal static class SoapUiXmlDocIds
	{
		/// <summary>
		/// Gets the member id of a type.
		/// </summary>
		/// <param name="type">The type. A constructed generic type is reduced to its generic type definition.</param>
		/// <returns>The id, for example "T:Ns.Outer`1.Inner", or <c>null</c> when the type has no id (a generic parameter).</returns>
		public static string? ForType(Type type)
		{
			var name = TypeDefinitionName(type);
			return name == null ? null : "T:" + name;
		}

		/// <summary>
		/// Gets the member id of a method.
		/// </summary>
		/// <param name="method">The method. A method of a constructed generic type is reduced to the method of its generic type definition.</param>
		/// <returns>The id, for example "M:Ns.Type.Method(System.String,System.Int32)", or <c>null</c> when the method has no id.</returns>
		public static string? ForMethod(MethodInfo method)
		{
			var definition = ToDefinition(method);
			var declaringType = definition.DeclaringType;
			if (declaringType == null)
			{
				return null;
			}

			var typeName = TypeDefinitionName(declaringType);
			if (typeName == null)
			{
				return null;
			}

			var builder = new StringBuilder("M:", 96);
			builder.Append(typeName).Append('.').Append(definition.Name);

			if (definition.IsGenericMethodDefinition)
			{
				builder.Append("``").Append(definition.GetGenericArguments().Length.ToString(CultureInfo.InvariantCulture));
			}

			var parameters = definition.GetParameters();
			if (parameters.Length > 0)
			{
				builder.Append('(');
				for (var i = 0; i < parameters.Length; i++)
				{
					if (i > 0)
					{
						builder.Append(',');
					}

					AppendType(builder, parameters[i].ParameterType);
				}

				builder.Append(')');
			}

			return builder.ToString();
		}

		private static MethodInfo ToDefinition(MethodInfo method)
		{
			var result = method;

			// The ids of the documentation describe the generic type definition: its methods use `0 and `1, not the type arguments.
			var declaringType = result.DeclaringType;
			if (declaringType != null && declaringType.IsConstructedGenericType)
			{
				// The method of the definition has the same metadata token as the method of the constructed type.
				const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
				foreach (var candidate in declaringType.GetGenericTypeDefinition().GetMethods(all))
				{
					if (candidate.MetadataToken == result.MetadataToken)
					{
						result = candidate;
						break;
					}
				}
			}

			if (result.IsGenericMethod && !result.IsGenericMethodDefinition)
			{
				result = result.GetGenericMethodDefinition();
			}

			return result;
		}

		// "Ns.Outer`1.Inner`1": the full name of the generic type definition, with '.' between the nested types.
		private static string? TypeDefinitionName(Type type)
		{
			var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
			return definition.FullName?.Replace('+', '.');
		}

		private static void AppendType(StringBuilder builder, Type type)
		{
			if (type.IsByRef)
			{
				AppendType(builder, type.GetElementType() ?? type);
				builder.Append('@');
				return;
			}

			if (type.IsPointer)
			{
				AppendType(builder, type.GetElementType() ?? type);
				builder.Append('*');
				return;
			}

			if (type.IsArray)
			{
				AppendType(builder, type.GetElementType() ?? type);
				AppendArrayRank(builder, type.GetArrayRank());
				return;
			}

			if (type.IsGenericParameter)
			{
				builder.Append(type.DeclaringMethod != null ? "``" : "`").Append(type.GenericParameterPosition.ToString(CultureInfo.InvariantCulture));
				return;
			}

			AppendNamedType(builder, type);
		}

		private static void AppendArrayRank(StringBuilder builder, int rank)
		{
			if (rank <= 1)
			{
				builder.Append("[]");
				return;
			}

			builder.Append('[');
			for (var i = 0; i < rank; i++)
			{
				if (i > 0)
				{
					builder.Append(',');
				}

				builder.Append("0:");
			}

			builder.Append(']');
		}

		// A class, struct, interface, enum or delegate, constructed or not, possibly nested in generic types.
		private static void AppendNamedType(StringBuilder builder, Type type)
		{
			var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
			var arguments = type.IsConstructedGenericType ? type.GetGenericArguments() : Type.EmptyTypes;

			// The declaring types, outermost first. For a nested type the arguments of the outer types come first in the argument list.
			var chain = new List<Type>();
			for (var current = definition; current != null; current = current.DeclaringType)
			{
				chain.Insert(0, current);
			}

			var ns = chain[0].Namespace;
			if (!string.IsNullOrEmpty(ns))
			{
				builder.Append(ns).Append('.');
			}

			var offset = 0;
			for (var i = 0; i < chain.Count; i++)
			{
				var name = chain[i].Name;
				var arity = 0;
				var tick = name.IndexOf('`');
				if (tick >= 0)
				{
					if (!int.TryParse(name.Substring(tick + 1), NumberStyles.None, CultureInfo.InvariantCulture, out arity))
					{
						arity = 0;
					}

					name = name.Substring(0, tick);
				}

				builder.Append(name);
				if (arity > 0 && arguments.Length >= offset + arity)
				{
					builder.Append('{');
					for (var j = 0; j < arity; j++)
					{
						if (j > 0)
						{
							builder.Append(',');
						}

						AppendType(builder, arguments[offset + j]);
					}

					builder.Append('}');
				}

				offset += arity;
				if (i < chain.Count - 1)
				{
					builder.Append('.');
				}
			}
		}
	}
}
