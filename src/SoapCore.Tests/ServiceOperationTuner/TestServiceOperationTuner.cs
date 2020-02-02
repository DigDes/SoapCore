using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SoapCore;
using SoapCore.Extensibility;

namespace SoapCore.Tests.ServiceOperationTuner
{
	public class TestServiceOperationTuner : IServiceOperationTuner
	{
		public static bool IsCalled { get; private set; }
		public static bool IsSetPingValue { get; private set; }
		public static bool IsBodyAvailable { get; private set; }

		public static void Reset()
		{
			IsCalled = false;
			IsSetPingValue = false;
			IsBodyAvailable = false;
		}

		public void Tune(HttpContext httpContext, object serviceInstance, ServiceModel.OperationDescription operation)
		{
			IsCalled = true;
			if ((serviceInstance != null) && (serviceInstance is TestService)
				&& operation.Name.Equals("PingWithServiceOperationTuning"))
			{
				TestService service = serviceInstance as TestService;
				string result = string.Empty;

				StringValues pingValue;
				if (httpContext.Request.Headers.TryGetValue("ping_value", out pingValue))
				{
					result = pingValue[0];
				}

				if (httpContext.Request.Body.CanRead)
				{
					IsBodyAvailable = true;
				}

				service.SetPingResult(result);
				IsSetPingValue = true;
			}
		}
	}
}
