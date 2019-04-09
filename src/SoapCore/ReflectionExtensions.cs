using System;
using System.Linq;
using System.Reflection;

namespace SoapCore
{
	/// <summary>Extensions to <see cref="Type"/>.</summary>
	internal static class ReflectionExtensions
	{
		/// <summary>Searches for the public method with the specified name and generic arguments.</summary>
		/// <param name="type">The current <see cref="Type"/>.</param>
		/// <param name="name">The string containing the name of the public generic method to get.</param>
		/// <param name="typeArguments">
		/// An array of types to be substituted for the type parameters of the generic method definition.
		/// </param>
		/// <exception cref="AmbiguousMatchException">More than one suitable method is found.</exception>
		/// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
		/// <returns>
		/// A <see cref="MethodInfo"/> object representing the public constructed method formed by substituting the elements of <paramref name="typeArguments"/> for the type parameters.-or- <c>null</c>.
		/// </returns>
		internal static MethodInfo GetGenericMethod(this Type type, string name, params Type[] typeArguments)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (typeArguments == null)
			{
				throw new ArgumentNullException(nameof(typeArguments));
			}

			if (typeArguments.Any(t => t == null))
			{
				throw new ArgumentNullException(nameof(typeArguments));
			}

			var methods = type.GetMethods()
				.Where(method => method.IsPublic)
				.Where(method => method.IsGenericMethod)
				.Where(method => method.Name == name)
				.Where(method =>
				{
					// check if genericArguments match with typeArguments
					var genericArguments = method.GetGenericArguments();
					if (genericArguments.Length != typeArguments.Length)
					{
						return false;
					}
					for (var i = 0; i < genericArguments.Length; i++)
					{
						var genericArgument = genericArguments[i];
						var typeArgument = typeArguments[i];
						if (!genericArgument.GetGenericParameterConstraints().All(constraint => constraint.IsAssignableFrom(typeArgument)))
						{
							return false;
						}
					}
					return true;
				});
			MethodInfo result = null;
			foreach (var method in methods)
			{
				if (result != null)
				{
					throw new AmbiguousMatchException();
				}

				result = method;
			}

			return result?.MakeGenericMethod(typeArguments);
		}
	}
}
