using System;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Extensibility;

namespace SoapCore.Tests.EndpointRegistry
{
	// https://github.com/DigDes/SoapCore/issues/1190
	[TestClass]
	public class SoapEndpointRegistryTests
	{
		[TestMethod]
		public async Task MiddlewareMode_RecordsSnapshotWithSoapCoreDefaults()
		{
			using var host = CreateHost(
				services => services.AddSoapEndpointRegistry(),
				app => app.UseSoapEndpoint<TestService, RegistryTestMessage>(opt => opt.Path = "/Service.svc"));

			var endpoint = host.Services.GetRequiredService<ISoapEndpointRegistry>().GetAll().Single();

			Assert.AreEqual("/Service.svc", endpoint.Path);
			Assert.AreEqual(typeof(TestService), endpoint.ServiceType);
			Assert.AreEqual(typeof(RegistryTestMessage), endpoint.MessageType);
			Assert.AreEqual(SoapSerializer.DataContractSerializer, endpoint.Serializer);
			Assert.IsNull(endpoint.CustomSerializerType);
			Assert.IsFalse(endpoint.CaseInsensitivePath);
			Assert.IsFalse(endpoint.GenerateSoapActionWithoutContractName);
			CollectionAssert.AreEqual(new[] { MessageVersion.Soap11 }, endpoint.MessageVersions.ToArray());

			await AssertPingSucceedsAsync(host, "/Service.svc");
		}

		[TestMethod]
		public async Task EndpointRouting_RecordsOneEntryPerCallInCallOrder()
		{
			using var host = CreateHost(
				services => services.AddSoapEndpointRegistry(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						endpoints.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
						endpoints.UseSoapEndpoint<TestService>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
						endpoints.UseSoapEndpoint(typeof(TestService), opt =>
						{
							opt.Path = "/Multi.svc";
							opt.CaseInsensitivePath = true;
							opt.GenerateSoapActionWithoutContractName = true;
							opt.EncoderOptions = new[]
							{
								new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11 },
								new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 },
								new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11, BindingName = "Second" },
							};
						});
					});
				});

			var endpoints = host.Services.GetRequiredService<ISoapEndpointRegistry>().GetAll();

			Assert.AreEqual(3, endpoints.Count);
			CollectionAssert.AreEqual(
				new[] { "/Service.svc", "/Service.asmx", "/Multi.svc" },
				endpoints.Select(e => e.Path).ToArray());
			CollectionAssert.AreEqual(
				new[] { SoapSerializer.DataContractSerializer, SoapSerializer.XmlSerializer, SoapSerializer.DataContractSerializer },
				endpoints.Select(e => e.Serializer).ToArray());
			Assert.IsTrue(endpoints[2].CaseInsensitivePath);
			Assert.IsTrue(endpoints[2].GenerateSoapActionWithoutContractName);
			CollectionAssert.AreEqual(
				new[] { MessageVersion.Soap11, MessageVersion.Soap12WSAddressing10 },
				endpoints[2].MessageVersions.ToArray());

			await AssertPingSucceedsAsync(host, "/Service.svc");
		}

		[TestMethod]
		public async Task SamePathInExclusiveBranches_RecordsBothEntriesInCallOrder()
		{
			using var host = CreateHost(
				services => services.AddSoapEndpointRegistry(),
				app =>
				{
					app.UseWhen(
						context => context.Request.Headers.ContainsKey("X-Soap12"),
						branch => branch.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 }, SoapSerializer.DataContractSerializer));
					app.UseWhen(
						context => !context.Request.Headers.ContainsKey("X-Soap12"),
						branch => branch.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11 }, SoapSerializer.DataContractSerializer));
				});

			var endpoints = host.Services.GetRequiredService<ISoapEndpointRegistry>().GetAll();

			Assert.AreEqual(2, endpoints.Count);
			Assert.IsTrue(endpoints.All(e => e.Path == "/Service.svc"));
			Assert.AreEqual(MessageVersion.Soap12WSAddressing10, endpoints[0].MessageVersions.Single());
			Assert.AreEqual(MessageVersion.Soap11, endpoints[1].MessageVersions.Single());

			await AssertPingSucceedsAsync(host, "/Service.svc");
		}

		[TestMethod]
		[DataRow(false)]
		[DataRow(true)]
		public async Task UserRegisteredRegistry_TakesPrecedenceAndReceivesEntries(bool useEndpointRouting)
		{
			var recorder = new RecordingSoapEndpointRegistry();

			using var host = CreateHost(
				services =>
				{
					services.AddSingleton<ISoapEndpointRegistry>(recorder);
					services.AddSoapEndpointRegistry(); // TryAdd: must not replace the user's registry
				},
				app => MapTestService(app, useEndpointRouting, "/Service.svc"));

			Assert.AreSame(recorder, host.Services.GetRequiredService<ISoapEndpointRegistry>());
			Assert.AreEqual("/Service.svc", recorder.Registered.Single().Path);

			await AssertPingSucceedsAsync(host, "/Service.svc");
		}

		[TestMethod]
		public async Task MapBranch_RecordsPathRelativeToBranch()
		{
			using var host = CreateHost(
				services => services.AddSoapEndpointRegistry(),
				app => app.Map("/soap", branch => branch.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer)));

			var endpoint = host.Services.GetRequiredService<ISoapEndpointRegistry>().GetAll().Single();

			Assert.AreEqual("/Service.svc", endpoint.Path);

			await AssertPingSucceedsAsync(host, "/soap/Service.svc");
		}

		private static TestServer CreateHost(Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configure)
		{
			var webHostBuilder = new WebHostBuilder()
				.ConfigureServices(services =>
				{
					services.AddRouting();
					configureServices(services); // before AddSoapCore: covers "registry registered before AddSoapCore"
					services.AddSoapCore();
					services.TryAddSingleton<TestService>();
				})
				.Configure(configure);

			return new TestServer(webHostBuilder);
		}

		private static void MapTestService(IApplicationBuilder app, bool useEndpointRouting, string path)
		{
			if (useEndpointRouting)
			{
				app.UseRouting();
				app.UseEndpoints(endpoints => endpoints.UseSoapEndpoint<TestService>(path, new SoapEncoderOptions(), SoapSerializer.DataContractSerializer));
			}
			else
			{
				app.UseSoapEndpoint<TestService>(path, new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
			}
		}

		private static async Task AssertPingSucceedsAsync(TestServer host, string path)
		{
			const string body = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soapenv:Body>
    <Ping xmlns=""http://tempuri.org/""><s>registry</s></Ping>
  </soapenv:Body>
</soapenv:Envelope>";

			using var content = new StringContent(body, Encoding.UTF8, "text/xml");
			using var response = await host.CreateRequest(path).AddHeader("SOAPAction", @"""Ping""").And(message => message.Content = content).PostAsync();

			response.EnsureSuccessStatusCode();
			StringAssert.Contains(await response.Content.ReadAsStringAsync(), "registry");
		}
	}
}
