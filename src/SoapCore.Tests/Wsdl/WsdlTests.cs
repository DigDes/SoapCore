using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Tests.Wsdl.Services;

namespace SoapCore.Tests.Wsdl
{
	[TestClass]
	public class WsdlTests
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		[TestMethod]
		public void CheckTaskReturnMethod()
		{
			string serviceUrl = "http://localhost:5053";
			StartService(typeof(TaskNoReturnService), serviceUrl);
			var wsdl = GetWsdl(serviceUrl);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

		[TestMethod]
		public void CheckDataContractContainsItself()
		{
			string serviceUrl = "http://localhost:5054";
			StartService(typeof(DataContractContainsItselfService), serviceUrl);
			var wsdl = GetWsdl(serviceUrl);
			Trace.TraceInformation(wsdl);
			Assert.IsNotNull(wsdl);
			StopServer();
		}

        [TestMethod]
        public void CheckDataContractCircularReference()
        {
            StartService(typeof(DataContractCircularReferenceService));
            var wsdl = GetWsdl();
            Trace.TraceInformation(wsdl);
            Assert.IsNotNull(wsdl);
            StopServer();
        }

        [TestCleanup]
        public void StopServer()
        {
            _cancellationTokenSource.Cancel();
        }

		private string GetWsdl(string serviceUrl, string serviceName = "Service.svc")
		{
			using (var httpClient = new HttpClient())
			{
				return httpClient.GetStringAsync(string.Format("{0}/{1}?wsdl", serviceUrl, serviceName)).Result;
			}
		}

		private void StartService(Type serviceType, string serviceUrl)
		{
			Task.Run(async () =>
			{
				var host = new WebHostBuilder()
					.UseKestrel()
					.UseUrls(serviceUrl)
					.ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)))
					.UseStartup<Startup>()
					.Build();
				await host.RunAsync(_cancellationTokenSource.Token);
			}).Wait(1000);
		}
	}
}
