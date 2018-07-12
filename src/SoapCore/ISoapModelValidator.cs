using System.Reflection;

namespace SoapCore
{
	public interface ISoapModelValidator
	{
		void OnModelBound(MethodInfo methodInfo, object[] prms);
	}
}
