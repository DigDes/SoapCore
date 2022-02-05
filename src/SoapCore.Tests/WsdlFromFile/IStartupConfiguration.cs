using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore.Tests.WsdlFromFile
{
	public interface IStartupConfiguration
	{
		Type ServiceType { get; }

		string WsdlFile { get; }
	}

	public class StartupConfiguration : IStartupConfiguration
	{
		public StartupConfiguration(Type serviceType, string wsdlFile)
		{
			ServiceType = serviceType;
			WsdlFile = wsdlFile;
		}

		public Type ServiceType { get; }

		public string WsdlFile { get; }
	}
}
