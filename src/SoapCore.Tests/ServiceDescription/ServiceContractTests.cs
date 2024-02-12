using System;
using System.Collections.Generic;
using System.Text;
using SoapCore.ServiceModel;
using Xunit;

namespace SoapCore.Tests
{
	public class ServiceContractTests
	{
		[Fact]
		public void TestFallbackOfServiceNameToTypeName()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithoutName), false);

			Assert.Equal("IServiceWithoutName", serviceDescription.ServiceName);
		}

		[Fact]
		public void TestExplicitlySetServiceName()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithName), false);

			Assert.Equal("MyServiceWithName", serviceDescription.ServiceName);
		}
	}
}
