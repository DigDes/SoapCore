using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Extensibility;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// Shared plumbing of the SoapCore.UI tests: host creation, HTTP helpers, JSON helpers and the security header assertions.
	public static class UiTestHost
	{
		public const string ExpectedCsp = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'; object-src 'none'";

		public const string ApiPath = "/soap-ui/api/services";

		// Services are configured before the SoapCore ones, like the registry tests do.
		// The default environment is Production unless environmentName says otherwise.
		public static TestServer Create(Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configure, string environmentName = null)
		{
			var webHostBuilder = new WebHostBuilder()
				.ConfigureServices(services =>
				{
					services.AddRouting();
					configureServices?.Invoke(services);
					services.AddSoapCore();
					services.TryAddSingleton<TestService>();
					services.TryAddSingleton<UiTestService>();
					services.TryAddSingleton<UiShapeService>();
				})
				.Configure(configure);

			if (!string.IsNullOrEmpty(environmentName))
			{
				webHostBuilder.UseEnvironment(environmentName);
			}

			return new TestServer(webHostBuilder);
		}

		// The standard pipeline: AddSoapCoreUi(), routing, the SOAP endpoints of mapSoap (if any) and the UI at /soap-ui.
		public static TestServer CreateWithUi(Action<IEndpointRouteBuilder> mapSoap, Action<SoapUiOptions> configureUi = null)
		{
			return Create(
				services => services.AddSoapCoreUi(configureUi),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						mapSoap?.Invoke(endpoints);
						endpoints.MapSoapCoreUi();
					});
				});
		}

		public static SoapEncoderOptions[] Soap11And12()
		{
			return new[]
			{
				new SoapEncoderOptions { MessageVersion = MessageVersion.Soap11 },
				new SoapEncoderOptions { MessageVersion = MessageVersion.Soap12WSAddressing10 },
			};
		}

		// /Service.svc (DataContractSerializer, SOAP 1.1 and 1.2) and /Service.asmx (XmlSerializer, SOAP 1.1), both TestService.
		public static void MapDefaultSoapEndpoints(IEndpointRouteBuilder endpoints)
		{
			endpoints.UseSoapEndpoint<TestService>("/Service.svc", Soap11And12(), SoapSerializer.DataContractSerializer);
			endpoints.UseSoapEndpoint<TestService>("/Service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);
		}

		// Records an endpoint straight in the registry, without hosting a SOAP endpoint. The UI reads the registry per request.
		public static void Register(TestServer host, string path, Type serviceType)
		{
			host.Services.GetRequiredService<ISoapEndpointRegistry>().Register(new SoapEndpointInfo
			{
				Path = path,
				ServiceType = serviceType,
				MessageVersions = new[] { MessageVersion.Soap11 },
			});
		}

		// Sends the request to the exact URL given (rawPath starts with '/'), so encoded and odd paths reach the server untouched.
		public static async Task<HttpResponseMessage> SendAsync(TestServer host, HttpMethod method, string rawPath, Action<HttpRequestMessage> configureRequest = null)
		{
			using var client = host.CreateClient();
			using var request = new HttpRequestMessage(method, new Uri("http://localhost" + rawPath));
			configureRequest?.Invoke(request);

			return await client.SendAsync(request);
		}

		public static Task<HttpResponseMessage> GetRawAsync(TestServer host, string rawPath)
		{
			return SendAsync(host, HttpMethod.Get, rawPath);
		}

		public static async Task<string> GetTextAsync(TestServer host, string rawPath)
		{
			using var response = await GetRawAsync(host, rawPath);
			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "GET " + rawPath);

			return await response.Content.ReadAsStringAsync();
		}

		public static async Task<JsonDocument> GetJsonAsync(TestServer host, string rawPath)
		{
			return JsonDocument.Parse(await GetTextAsync(host, rawPath));
		}

		public static string[] PropertyNames(JsonElement element)
		{
			return element.EnumerateObject().Select(property => property.Name).ToArray();
		}

		public static string[] Strings(JsonElement array)
		{
			return array.EnumerateArray().Select(item => item.GetString()).ToArray();
		}

		public static string[] ServiceIds(JsonDocument document)
		{
			return document.RootElement.GetProperty("services").EnumerateArray().Select(service => service.GetProperty("id").GetString()).ToArray();
		}

		public static string[] OperationNames(JsonElement service)
		{
			return service.GetProperty("operations").EnumerateArray().Select(operation => operation.GetProperty("name").GetString()).ToArray();
		}

		public static JsonElement GetService(JsonDocument document, int index)
		{
			return document.RootElement.GetProperty("services")[index];
		}

		public static JsonElement GetOperation(JsonElement service, string name)
		{
			return service.GetProperty("operations").EnumerateArray().Single(operation => operation.GetProperty("name").GetString() == name);
		}

		public static string GetSingleHeader(HttpResponseMessage response, string name)
		{
			IEnumerable<string> values;
			if (!response.Headers.TryGetValues(name, out values) && !response.Content.Headers.TryGetValues(name, out values))
			{
				Assert.Fail("The response has no " + name + " header.");
			}

			var all = values.ToArray();
			Assert.AreEqual(1, all.Length, "Expected exactly one " + name + " header.");

			return all[0];
		}

		// The headers every UI response written by SoapCore.UI must carry (200, 304 and 308 responses).
		public static void AssertSecurityHeaders(HttpResponseMessage response, bool noStore)
		{
			Assert.AreEqual(ExpectedCsp, GetSingleHeader(response, "Content-Security-Policy"));
			Assert.AreEqual("nosniff", GetSingleHeader(response, "X-Content-Type-Options"));
			Assert.AreEqual("no-referrer", GetSingleHeader(response, "Referrer-Policy"));
			Assert.AreEqual("DENY", GetSingleHeader(response, "X-Frame-Options"));

			Assert.IsNotNull(response.Headers.CacheControl, "Missing Cache-Control header.");
			Assert.IsTrue(response.Headers.CacheControl.NoCache, "Cache-Control must contain no-cache.");
			Assert.AreEqual(noStore, response.Headers.CacheControl.NoStore, "Cache-Control no-store.");
		}
	}
}
