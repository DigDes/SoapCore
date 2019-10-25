using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore.Tests.Wsdl
{
	public interface IStartupConfiguration
	{
		Type ServiceType { get; }
	}

	public class StartupConfiguration : IStartupConfiguration
	{
		public StartupConfiguration(Type serviceType)
		{
			ServiceType = serviceType;
		}

		public Type ServiceType { get; }
	}
}
