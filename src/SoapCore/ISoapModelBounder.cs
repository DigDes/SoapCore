using System.Reflection;

namespace SoapCore
{
	public interface ISoapModelBounder
	{
		void OnModelBound(MethodInfo methodInfo, object[] prms);
	}
}
