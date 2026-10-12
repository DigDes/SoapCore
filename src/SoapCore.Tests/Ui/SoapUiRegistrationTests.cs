using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SoapCore.Extensibility;
using SoapCore.Tests.EndpointRegistry;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// AddSoapCoreUi / MapSoapCoreUi: registration, argument validation and option validation.
	[TestClass]
	public class SoapUiRegistrationTests
	{
		[TestMethod]
		public void MapSoapCoreUi_WithoutAddSoapCoreUi_ThrowsInvalidOperationException()
		{
			var exception = Assert.ThrowsException<InvalidOperationException>(() => UiTestHost.Create(
				null,
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				}));

			StringAssert.Contains(exception.Message, "AddSoapCoreUi");
		}

		[TestMethod]
		[DataRow("")]
		[DataRow(" ")]
		[DataRow("soap-ui")]
		[DataRow("/soap-ui/")]
		[DataRow("/")]
		[DataRow("/soap ui")]
		[DataRow("/soap-ui?x")]
		[DataRow("/{x}")]
		[DataRow("//x")]
		[DataRow("/a/../b")]
		[DataRow("/a/./b")]
		[DataRow("/soap\n")]
		[DataRow("/soap\r\n")]
		public void MapSoapCoreUi_InvalidPattern_ThrowsArgumentException(string pattern)
		{
			Assert.ThrowsException<ArgumentException>(() => UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi(pattern));
				}));
		}

		[TestMethod]
		public void MapSoapCoreUi_NullPattern_ThrowsArgumentNullException()
		{
			Assert.ThrowsException<ArgumentNullException>(() => UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi(null));
				}));
		}

		[TestMethod]
		public void MapSoapCoreUi_NullEndpoints_ThrowsArgumentNullException()
		{
			IEndpointRouteBuilder endpoints = null;

			Assert.ThrowsException<ArgumentNullException>(() => endpoints.MapSoapCoreUi());
		}

		[TestMethod]
		public void AddSoapCoreUi_NullServices_ThrowsArgumentNullException()
		{
			IServiceCollection services = null;

			Assert.ThrowsException<ArgumentNullException>(() => services.AddSoapCoreUi());
		}

		[TestMethod]
		[DataRow("")]
		[DataRow(" ")]
		[DataRow("a\tb")]
		[DataRow("a\nb")]
		public void AddSoapCoreUi_InvalidTitle_ThrowsOptionsValidationException(string title)
		{
			Assert.ThrowsException<OptionsValidationException>(() => UiTestHost.CreateWithUi(null, options => options.Title = title));
		}

		[TestMethod]
		public void AddSoapCoreUi_TitleLongerThan100Characters_ThrowsOptionsValidationException()
		{
			var title = new string('x', 101);

			Assert.ThrowsException<OptionsValidationException>(() => UiTestHost.CreateWithUi(null, options => options.Title = title));
		}

		[TestMethod]
		public async Task AddSoapCoreUi_TitleOf100Characters_IsAccepted()
		{
			var title = new string('x', 100);
			using var host = UiTestHost.CreateWithUi(null, options => options.Title = title);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(title, document.RootElement.GetProperty("title").GetString());
		}

		[TestMethod]
		[DataRow("soap")]
		[DataRow("/soap/")]
		[DataRow("/a//b")]
		[DataRow("/a b")]
		[DataRow("/a?b")]
		[DataRow("//evil.com")]
		[DataRow("https://evil.com")]
		[DataRow("/../x")]
		[DataRow("/soap\n")]
		[DataRow("/soap\r\n")]
		public void AddSoapCoreUi_InvalidEndpointPathPrefix_ThrowsOptionsValidationException(string prefix)
		{
			Assert.ThrowsException<OptionsValidationException>(() => UiTestHost.CreateWithUi(null, options => options.EndpointPathPrefix = prefix));
		}

		[TestMethod]
		[DataRow("")]
		[DataRow("/soap")]
		[DataRow("/a/b-c_d.e")]
		public async Task AddSoapCoreUi_ValidEndpointPathPrefix_IsAccepted(string prefix)
		{
			using var host = UiTestHost.CreateWithUi(null, options => options.EndpointPathPrefix = prefix);

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[TestMethod]
		public void AddSoapCoreUi_RegistersDefaultRegistry()
		{
			using var host = UiTestHost.CreateWithUi(null);

			Assert.IsNotNull(host.Services.GetService<ISoapEndpointRegistry>());
		}

		[TestMethod]
		[DataRow(true)]
		[DataRow(false)]
		public async Task AddSoapCoreUi_UserRegistry_IsNotReplaced(bool registeredBeforeUi)
		{
			var recorder = new RecordingSoapEndpointRegistry();

			using var host = UiTestHost.Create(
				services =>
				{
					if (registeredBeforeUi)
					{
						services.TryAddSingleton<ISoapEndpointRegistry>(recorder);
						services.AddSoapCoreUi();
					}
					else
					{
						services.AddSoapCoreUi();
						services.AddSingleton<ISoapEndpointRegistry>(recorder);
					}
				},
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints =>
					{
						endpoints.UseSoapEndpoint<TestService>("/Service.svc", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);
						endpoints.MapSoapCoreUi();
					});
				});

			Assert.AreSame(recorder, host.Services.GetRequiredService<ISoapEndpointRegistry>());
			Assert.AreEqual(1, recorder.Registered.Count);

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			CollectionAssert.AreEqual(new[] { "service-svc" }, UiTestHost.ServiceIds(document));
		}

		[TestMethod]
		public async Task AddSoapCoreUi_CalledTwice_AppliesBothDelegates()
		{
			using var host = UiTestHost.Create(
				services =>
				{
					services.AddSoapCoreUi(options =>
					{
						options.Title = "First";
						options.EnableTryItOut = false;
					});
					services.AddSoapCoreUi(options => options.Title = "Second");
				},
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi());
				});

			using var document = await UiTestHost.GetJsonAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual("Second", document.RootElement.GetProperty("title").GetString());
			Assert.IsFalse(document.RootElement.GetProperty("tryItOutEnabled").GetBoolean(), "The first delegate still applies.");
		}

		[TestMethod]
		public async Task MapSoapCoreUi_ReturnsConventionBuilder_AllowingChaining()
		{
			using var host = UiTestHost.Create(
				services => services.AddSoapCoreUi(),
				app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapSoapCoreUi().RequireHost("localhost"));
				});

			using var response = await UiTestHost.GetRawAsync(host, UiTestHost.ApiPath);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}
	}
}
