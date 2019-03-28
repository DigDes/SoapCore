using System;
using System.Collections;
using System.Collections.Generic;

namespace SoapCore
{
	internal class TypesComparer : IEqualityComparer<Type>
	{
		private readonly Func<Type, string> _getTypeNameFunc;

		public TypesComparer(Func<Type, string> getTypeNameFunc)
		{
			_getTypeNameFunc = getTypeNameFunc;
		}

		public bool Equals(Type x, Type y)
		{
			return _getTypeNameFunc(x) == _getTypeNameFunc(y);
		}

		public int GetHashCode(Type obj)
		{
			return _getTypeNameFunc(obj).GetHashCode();
		}
	}
}
