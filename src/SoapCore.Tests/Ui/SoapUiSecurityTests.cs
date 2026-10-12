using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// Protecting the UI with the standard endpoint conventions, and what the UI does when it is left open.
	[TestClass]
	public class SoapUiSecurityTests
	{
		[TestMethod]
		[DataRow("/soap-ui", HttpStatusCode.PermanentRedirect)]
		[DataRow("/soap-ui/", HttpStatusCode.OK)]
		[DataRow("/soap-ui/app.js", HttpStatusCode.OK)]
		[DataRow("/soap-ui/wsdl-parser.js", HttpStatusCode.OK)]
		[DataRow("/soap-ui/form-editor.js", HttpStatusCode.OK)]
		[DataRow("/soap-ui/xml-view.js", HttpStatusCode.OK)]
		[DataRow("/soap-ui/style.css", HttpStatusCode.OK)]
		[DataRow("/soap-ui/form-editor.css", HttpStatusCode.OK)]
		[DataRow("/soap-ui/api/services", HttpStatusCode.OK)]
		public async Task RequireAuthorization_AnonymousGets401_AuthenticatedGetsTheResource(string path, HttpStatusCode authenticatedStatus)
		{
			using var host = CreateProtectedHost(null);

			using var anonymous = await UiTestHost.GetRawAsync(host, path);
			using var authenticated = await UiTestHost.SendAsync(host, HttpMethod.Get, path, request => request.Headers.Add(TestAuthHandler.UserHeader, "alice"));

			Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode, "anonymous " + path);
			Assert.AreEqual(authenticatedStatus, authenticated.StatusCode, "authenticated " + path);
		}

		[TestMethod]
		public async Task RequireAuthorization_DoesNotProtectSoapEndpoints()
		{
			// The convention returned by MapSoapCoreUi covers the UI routes only, never the SOAP endpoints.
			using var host = CreateProtectedHost(endpoints => endpoints.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer));

			using var wsdl = await UiTestHost.GetRawAsync(host, "/Service.svc?wsdl");
			using var api = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.OK, wsdl.StatusCode);
			Assert.AreEqual(HttpStatusCode.Unauthorized, api.StatusCode);
		}

		[TestMethod]
		public async Task RequireHost_RestrictsUiToConfiguredHost()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi().RequireHost("example.com"));
				});

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
		}

		[TestMethod]
		public async Task DefaultCorsPolicy_DoesNotExposeUiCrossOrigin()
		{
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
				},
				app =>
				{
					app.UseRouting();
					app.UseCors();
					app.UseEndpoints(endpoints =>
					{
						endpoints.MapSoapCoreUi();
						endpoints.MapGet("/plain", context => context.Response.WriteAsync("plain"));
					});
				});

			using var plain = await UiTestHost.SendAsync(host, HttpMethod.Get, "/plain", request => request.Headers.Add("Origin", "http://evil.example"));
			using var api = await UiTestHost.SendAsync(host, HttpMethod.Get, UiTestHost.ApiPath, request => request.Headers.Add("Origin", "http://evil.example"));

			// Control: the host-wide policy is active for an ordinary endpoint.
			Assert.IsTrue(plain.Headers.Contains("Access-Control-Allow-Origin"), "Control: the host-wide CORS policy must be active.");

			// The UI refuses a request that names another origin, whatever the host-wide policy says: the answer is an empty 403.
			// (The host-wide CORS middleware runs first and may still add its allow-origin header to that empty answer.)
			// A CORS preflight is not asserted: it is answered by the host-wide CORS middleware before any endpoint runs.
			Assert.AreEqual(HttpStatusCode.Forbidden, api.StatusCode);
			Assert.AreEqual(string.Empty, await api.Content.ReadAsStringAsync(), "The UI API must not be readable cross-origin.");
		}

		[TestMethod]
		[DataRow("GET", "/soap-ui/api/services")]
		[DataRow("HEAD", "/soap-ui/api/services")]
		[DataRow("GET", "/soap-ui/")]
		[DataRow("GET", "/soap-ui/app.js")]
		[DataRow("GET", "/soap-ui/form-editor.js")]
		[DataRow("GET", "/soap-ui/xml-view.js")]
		[DataRow("GET", "/soap-ui/form-editor.css")]
		[DataRow("HEAD", "/soap-ui/form-editor.js")]
		[DataRow("GET", "/soap-ui")]
		public async Task ForeignOrigin_IsRefusedWith403KeepingSecurityHeaders(string method, string path)
		{
			using var host = UiTestHost.CreateWithUi(UiTestHost.MapDefaultSoapEndpoints);

			using var response = await UiTestHost.SendAsync(host, new HttpMethod(method), path, request => request.Headers.Add("Origin", "http://evil.example"));

			Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, method + " " + path);
			Assert.AreEqual(0, response.Content.Headers.ContentLength ?? 0, "The refusal has no body.");
			UiTestHost.AssertSecurityHeaders(response, true);
		}

		[TestMethod]
		[DataRow("http://localhost")]
		[DataRow("HTTP://LOCALHOST")]
		public async Task SameOriginRequestCarryingAnOriginHeader_IsServed(string origin)
		{
			using var host = UiTestHost.CreateWithUi(UiTestHost.MapDefaultSoapEndpoints);

			using var response = await UiTestHost.SendAsync(host, HttpMethod.Get, UiTestHost.ApiPath, request => request.Headers.Add("Origin", origin));

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			UiTestHost.AssertSecurityHeaders(response, true);
		}

		[TestMethod]
		[DataRow("http://localhost:81")]
		[DataRow("https://localhost")]
		[DataRow("http://localhost.evil.example")]
		[DataRow("null")]
		public async Task OriginThatDiffersInPortSchemeOrHost_IsRefused(string origin)
		{
			using var host = UiTestHost.CreateWithUi(null);

			using var response = await UiTestHost.SendAsync(host, HttpMethod.Get, UiTestHost.ApiPath, request => request.Headers.TryAddWithoutValidation("Origin", origin));

			Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, origin);
		}

		[TestMethod]
		public async Task RequireCorsChainedAfterMap_RestoresCorsForAllowedOrigin()
		{
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddCors(options => options.AddPolicy("p", policy => policy.WithOrigins("http://ok.example")));
				},
				app =>
				{
					app.UseRouting();
					app.UseCors();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi().RequireCors("p"));
				});

			using var response = await UiTestHost.SendAsync(host, HttpMethod.Get, UiTestHost.ApiPath, request => request.Headers.Add("Origin", "http://ok.example"));

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual("http://ok.example", UiTestHost.GetSingleHeader(response, "Access-Control-Allow-Origin"));
		}

		[TestMethod]
		public async Task Anonymous_OutsideDevelopment_LogsOneWarning()
		{
			var logs = new ListLoggerProvider();
			using var host = CreateHostWithLogs(logs, null);

			using var first = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);
			using var second = await UiTestHost.GetRawAsync(host, "/soap-ui/");

			Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
			Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
			Assert.AreEqual(1, CountAnonymousWarnings(logs));
		}

		[TestMethod]
		public async Task Anonymous_InDevelopment_LogsNoWarning()
		{
			var logs = new ListLoggerProvider();
			using var host = CreateHostWithLogs(logs, "Development");

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual(0, CountAnonymousWarnings(logs));
		}

		[TestMethod]
		public async Task RequireAuthorization_OutsideDevelopment_LogsNoWarning()
		{
			var logs = new ListLoggerProvider();
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddSingleton<ILoggerProvider>(logs);
					AddTestAuthentication(services);
				},
				app =>
				{
					app.UseRouting();
					app.UseAuthentication();
					app.UseAuthorization();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi().RequireAuthorization());
				});

			using var response = await UiTestHost.SendAsync(host, HttpMethod.Get, UiTestHost.ApiPath, request => request.Headers.Add(TestAuthHandler.UserHeader, "alice"));

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual(0, CountAnonymousWarnings(logs));
		}

		[TestMethod]
		public async Task AuthorizationFallbackPolicy_OutsideDevelopment_LogsNoWarning()
		{
			// A FallbackPolicy protects endpoints that carry no authorization metadata, so the explorer is not left open.
			var logs = new ListLoggerProvider();
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddSingleton<ILoggerProvider>(logs);
					AddTestAuthentication(services);
					services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
				},
				app =>
				{
					app.UseRouting();
					app.UseAuthentication();
					app.UseAuthorization();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});

			using var anonymous = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);
			using var authenticated = await UiTestHost.SendAsync(host, HttpMethod.Get, UiTestHost.ApiPath, request => request.Headers.Add(TestAuthHandler.UserHeader, "alice"));

			Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
			Assert.AreEqual(HttpStatusCode.OK, authenticated.StatusCode);
			Assert.AreEqual(0, CountAnonymousWarnings(logs));
		}

		[TestMethod]
		public async Task AuthorizationFallbackPolicy_WithAllowAnonymousOnTheUi_StillLogsTheWarning()
		{
			var logs = new ListLoggerProvider();
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddSingleton<ILoggerProvider>(logs);
					AddTestAuthentication(services);
					services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
				},
				app =>
				{
					app.UseRouting();
					app.UseAuthentication();
					app.UseAuthorization();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi().AllowAnonymous());
				});

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual(1, CountAnonymousWarnings(logs));
		}

		private static void AddTestAuthentication(IServiceCollection services)
		{
			services.AddAuthentication(TestAuthHandler.SchemeName)
				.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
			services.AddAuthorization();
		}

		private static TestServer CreateProtectedHost(Action<IEndpointRouteBuilder> mapSoap)
		{
			return UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					AddTestAuthentication(services);
				},
				app =>
				{
					app.UseRouting();
					app.UseAuthentication();
					app.UseAuthorization();
					app.UseEndpoints(endpoints =>
					{
						mapSoap?.Invoke(endpoints);
						endpoints.MapSoapCoreUi().RequireAuthorization();
					});
				});
		}

		private static TestServer CreateHostWithLogs(ListLoggerProvider logs, string environmentName)
		{
			return UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi();
					services.AddSingleton<ILoggerProvider>(logs);
				},
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				},
				environmentName);
		}

		private static int CountAnonymousWarnings(ListLoggerProvider logs)
		{
			return logs.Entries.Count(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("without authorization", StringComparison.Ordinal));
		}
	}
}
