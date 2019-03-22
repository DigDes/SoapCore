using System;
using System.Collections.Generic;

namespace SoapCore.Tests
{
	internal class MockServiceProvider : IServiceProvider
	{
		private bool _isTrailingService = false;

		public MockServiceProvider(bool isTrailingService)
		{
			_isTrailingService = isTrailingService;
		}

		public object GetService(Type serviceType)
		{
			return _isTrailingService ? new List<TrailingServicePathTuner> { new TrailingServicePathTuner() } : new List<TrailingServicePathTuner>();
		}
	}
}
