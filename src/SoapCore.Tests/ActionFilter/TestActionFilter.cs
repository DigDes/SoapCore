using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SoapCore.Tests.ActionFilter
{
    class TestActionFilter: ActionFilterAttribute
    {
		public void OnSoapActionExecuting(string operationName, object[] allArgs, HttpContext httpContext, object result)
		{
			var complexModel = (ComplexModelInput)allArgs[0];
			complexModel.StringProperty += "MODIFIED BY TestActionFilter";
			complexModel.IntProperty = complexModel.IntProperty * 2;
		}
	}
}
