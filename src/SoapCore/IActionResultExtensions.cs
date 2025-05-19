using Microsoft.AspNetCore.Mvc;

namespace SoapCore
{
	public static class IActionResultExtensions
	{
		public static (int? StatusCode, object Value) ExtractResult(this IActionResult result)
		{
			return result switch
			{
				ObjectResult objectResult => (objectResult.StatusCode, objectResult.Value),
#if NETCOREAPP3_1_OR_GREATER
				JsonResult jsonResult => (200, jsonResult.Value), // JSON result defaults to 200 OK
#endif
				StatusCodeResult statusCodeResult => (statusCodeResult.StatusCode, null),
				ContentResult contentResult => (contentResult.StatusCode ?? 200, contentResult.Content),
				EmptyResult => (204, null), // No content
				_ => (null, null) // Unknown result type
			};
		}
	}
}
