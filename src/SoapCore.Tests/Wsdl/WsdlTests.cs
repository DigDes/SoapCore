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
        private const string ServiceUrl = "http://localhost:5052";
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        [TestMethod]
        public void CheckTaskReturnMethod()
        {
            StartService(typeof(TaskNoReturnService));
            var wsdl = GetWsdl();
            Trace.TraceInformation(wsdl);
            Assert.IsNotNull(wsdl);
            StopServer();
        }

        [TestMethod]
        public void CheckDataContractContainsItself()
        {
            StartService(typeof(DataContractContainsItselfService));
            var wsdl = GetWsdl();
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

        private string GetWsdl(string serviceName = "Service.svc")
        {
            using (var httpClient = new HttpClient())
            {
                return httpClient.GetStringAsync(string.Format("{0}/{1}?wsdl", ServiceUrl, serviceName)).Result;
            }
        }

        private void StartService(Type serviceType)
        {
            Task.Run(async () =>
            {
                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseUrls(ServiceUrl)
                    .ConfigureServices(services => services.AddSingleton<IStartupConfiguration>(new StartupConfiguration(serviceType)))
                    .UseStartup<Startup>()
                    .Build();
                await host.RunAsync(_cancellationTokenSource.Token);
            }).Wait(1000);
        }
    }
}
