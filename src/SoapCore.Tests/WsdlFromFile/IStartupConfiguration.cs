using System;
using System.Collections.Generic;
using System.Text;

namespace SoapCore.Tests.WsdlFromFile
{
	public interface IStartupConfiguration
	{
		string ServiceName { get; }

		Type ServiceType { get; }

		string TestFileFolder { get; }

		string WsdlFile { get; }
	}

	public class StartupConfiguration : IStartupConfiguration
	{
		public StartupConfiguration(string serviceName, Type serviceType, string testFileFolder, string wsdlFile)
		{
			ServiceName = serviceName;
			ServiceType = serviceType;
			TestFileFolder = testFileFolder;
			WsdlFile = wsdlFile;
		}

		public string ServiceName { get; }

		public Type ServiceType { get; }

		public string TestFileFolder { get; }

		public string WsdlFile { get; }
	}
}
