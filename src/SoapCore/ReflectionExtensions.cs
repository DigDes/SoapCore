using System;
using System.Collections.Generic;
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

		/// <summary>
		/// Gets the field or property members of the specific type.
		/// </summary>
		/// <param name="type">The type to look for field or property members for</param>
		/// <returns>An enumerable containing members which are fields or properties</returns>
		internal static IEnumerable<MemberInfo> GetPropertyOrFieldMembers(this Type type) =>
			type.GetFields()
				.Cast<MemberInfo>()
				.Concat(type.GetProperties());

		/// <summary>
		/// Gets the field or property type of a member. Returns null if the member is neither a field or
		/// a property member
		/// </summary>
		/// <param name="memberInfo">The member to get the field or property type</param>
		/// <returns>The return type of the member, null if it could not be determined</returns>
		internal static Type GetPropertyOrFieldType(this MemberInfo memberInfo)
		{
			if (memberInfo is FieldInfo fi)
			{
				return fi.FieldType;
			}

			if (memberInfo is PropertyInfo pi)
			{
				return pi.PropertyType;
			}

			return null;
		}

		internal static void SetValueToPropertyOrField(this MemberInfo memberInfo, object obj, object value)
		{
			if (memberInfo is FieldInfo fi)
			{
				fi.SetValue(obj, value);
			}
			else if (memberInfo is PropertyInfo pi)
			{
				pi.SetValue(obj, value);
			}
			else
			{
				throw new NotImplementedException("Cannot set value of parameter type from " + memberInfo.GetType()?.Name);
			}
		}

		internal static object GetPropertyOrFieldValue(this MemberInfo memberInfo, object obj)
		{
			if (memberInfo is FieldInfo fi)
			{
				return fi.GetValue(obj);
			}

			if (memberInfo is PropertyInfo pi)
			{
				return pi.GetValue(obj);
			}

			throw new NotImplementedException($"Unable to get value out of member with type {memberInfo.GetType()}");
		}

		internal static IEnumerable<MemberInfo> GetMembersWithAttribute<TAttribute>(this Type type)
			where TAttribute : Attribute
		{
			return GetPropertyOrFieldMembers(type).Where(x => x.GetCustomAttribute<TAttribute>() != null);
		}
	}
}
