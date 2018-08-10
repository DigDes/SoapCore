using System;
using System.IO;
using System.ServiceModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Models;
using Moq;

namespace SoapCore.Tests.Serialization
{
	public class SampleServiceFixture : IDisposable
	{
		public const int Port = 5050;
		public Mock<ISampleService> sampleServiceMock { get; private set; }
		public readonly ISampleService sampleServiceClient;
		private readonly IWebHost host;

		// todo: also test DataContractSerializer by using Service.svc endpoint
		// perhaps make two clients in fixture and use theory to iterate through

		public SampleServiceFixture()
		{
			// start service host

			this.host = new WebHostBuilder()
				.ConfigureServices(services =>
				{
					// init SampleService service mock
					this.sampleServiceMock = new Mock<ISampleService>();
					services.AddSingleton<ISampleService>(sampleServiceMock.Object);
					services.AddMvc();
				})
				.Configure(appBuilder =>
				{
					appBuilder.UseSoapEndpoint<ISampleService>("/Service.asmx", new BasicHttpBinding(), SoapSerializer.XmlSerializer);
					appBuilder.UseMvc();
				})
				.UseKestrel()
				.UseUrls($"http://*:{Port}")
				.UseContentRoot(Directory.GetCurrentDirectory())
				.Build();

#pragma warning disable 4014
			host.RunAsync();
#pragma warning restore 4014

			// make service client

			var binding = new BasicHttpBinding();
			var endpoint = new EndpointAddress(new Uri($"http://localhost:{Port}/Service.asmx"));
			var channelFactory = new ChannelFactory<ISampleService>(binding, endpoint);
			this.sampleServiceClient = channelFactory.CreateChannel();
		}

		public void Dispose()
		{
			this.host.StopAsync().GetAwaiter().GetResult();
		}
	}
}
