using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.ActionFilter
{
	internal class TestActionFilter : ActionFilterAttribute
	{
		public void OnSoapActionExecuting(string operationName, object[] allArgs, HttpContext httpContext, object result)
		{
			var complexModel = (ComplexModelInput)allArgs[0];
			complexModel.StringProperty += "MODIFIED BY TestActionFilter";
			complexModel.IntProperty = complexModel.IntProperty * 2;
		}
	}
}
