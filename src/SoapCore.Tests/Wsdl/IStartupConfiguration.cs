using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore.Tests.Wsdl
{
	public interface IStartupConfiguration
	{
		Type ServiceType { get; }

		string SchemeOverride { get; }
	}

	public class StartupConfiguration : IStartupConfiguration
	{
		public StartupConfiguration(Type serviceType, string schemeOverride)
		{
			ServiceType = serviceType;
			SchemeOverride = schemeOverride;
		}

		public Type ServiceType { get; }

		public string SchemeOverride { get; }
	}
}
