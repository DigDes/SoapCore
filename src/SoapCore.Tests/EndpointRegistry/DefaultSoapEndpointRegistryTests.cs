using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Extensibility;

namespace SoapCore.Tests.EndpointRegistry
{
	// https://github.com/DigDes/SoapCore/issues/1190
	[TestClass]
	public class DefaultSoapEndpointRegistryTests
	{
		[TestMethod]
		public void AddSoapCore_DoesNotEnableRegistry()
		{
			var services = new ServiceCollection();
			services.AddSoapCore();

			using var provider = services.BuildServiceProvider();

			Assert.IsNull(provider.GetService<ISoapEndpointRegistry>());
		}

		[TestMethod]
		public void AddSoapEndpointRegistry_RegistersOneSingleton()
		{
			var services = new ServiceCollection();
			services.AddSoapEndpointRegistry();
			services.AddSoapEndpointRegistry();

			var descriptor = services.Single(d => d.ServiceType == typeof(ISoapEndpointRegistry));
			Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);

			using var provider = services.BuildServiceProvider();

			Assert.AreSame(provider.GetRequiredService<ISoapEndpointRegistry>(), provider.GetRequiredService<ISoapEndpointRegistry>());
		}

		[TestMethod]
		public void DefaultRegistry_ValidatesAndReturnsReadOnlySnapshots()
		{
			var services = new ServiceCollection();
			services.AddSoapEndpointRegistry();

			using var provider = services.BuildServiceProvider();
			var registry = provider.GetRequiredService<ISoapEndpointRegistry>();

			Assert.ThrowsException<ArgumentNullException>(() => registry.Register(null));

			var first = new SoapEndpointInfo { Path = "/Service.svc" };
			var second = new SoapEndpointInfo { Path = "/Service.svc" };

			registry.Register(first);
			var snapshot = registry.GetAll();
			registry.Register(second);

			Assert.AreEqual(1, snapshot.Count);
			Assert.IsTrue(((ICollection<SoapEndpointInfo>)snapshot).IsReadOnly);

			var all = registry.GetAll();
			Assert.AreEqual(2, all.Count);
			Assert.AreSame(first, all[0]);
			Assert.AreSame(second, all[1]);
		}
	}
}
