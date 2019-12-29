using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SoapCore.Extensibility;

namespace SoapCore.Tests
{
	internal class MockModelBounder : ISoapModelBounder
	{
		public void OnModelBound(MethodInfo methodInfo, object[] prms)
		{
			return;
		}
	}
}
