using System;

namespace SoapCore.Tests
{
	internal class MockServiceProvider : IServiceProvider
	{
		private readonly bool _isTrailingService;

		public MockServiceProvider(bool isTrailingService)
		{
			_isTrailingService = isTrailingService;
		}

		public object GetService(Type serviceType)
		{
			return (serviceType == typeof(TrailingServicePathTuner) && _isTrailingService) ? new TrailingServicePathTuner() : null;
		}
	}
}
