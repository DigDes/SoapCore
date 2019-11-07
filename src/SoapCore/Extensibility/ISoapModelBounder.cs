using System.Reflection;

namespace SoapCore.Extensibility
{
	public interface ISoapModelBounder
	{
		void OnModelBound(MethodInfo methodInfo, object[] prms);
	}
}
