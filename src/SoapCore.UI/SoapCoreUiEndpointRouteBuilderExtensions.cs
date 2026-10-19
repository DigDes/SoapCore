using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SoapCore.UI
{
	/// <summary>
	/// Endpoint mapping for the SoapCore.UI explorer.
	/// </summary>
	public static class SoapCoreUiEndpointRouteBuilderExtensions
	{
		/// <summary>
		/// Maps the SoapCore.UI explorer under the given pattern.
		/// </summary>
		/// <param name="endpoints">The endpoint route builder, for example the one passed to <c>UseEndpoints</c> or a <c>WebApplication</c>.</param>
		/// <param name="pattern">
		/// The route prefix of the explorer. Must start with '/', have no trailing '/', and consist of segments of letters, digits, '.', '_', '~' or '-'.
		/// Defaults to '/soap-ui'.
		/// </param>
		/// <returns>
		/// A convention builder for the whole explorer. It is a route group, so its conventions apply to every explorer route:
		/// chain <c>RequireAuthorization()</c>, <c>RequireHost()</c> or <c>RequireCors()</c> on it.
		/// </returns>
		/// <remarks>
		/// <para>
		/// Only GET and HEAD are mapped, and the explorer only talks to SOAP endpoints on its own origin.
		/// Nothing is proxied by the server.
		/// </para>
		/// <para>
		/// The conventions of the returned builder protect the explorer routes only. The SOAP endpoints themselves are NOT covered by them.
		/// </para>
		/// <para>
		/// Cross-origin reads of the explorer are disabled by default: a GET or HEAD request that carries an <c>Origin</c> header for another
		/// origin is answered with 403, even when the host has a permissive host-wide CORS policy. The explorer itself never sends one.
		/// Chaining <c>.RequireCors(policy)</c> on the returned builder opts in and lifts this check.
		/// </para>
		/// <para>
		/// A warning is logged on the first request when the explorer is served without authorization outside the Development environment:
		/// no authorization requirement on its endpoints (<c>RequireAuthorization()</c>) and no authorization <c>FallbackPolicy</c>.
		/// The explorer never blocks a request because of it.
		/// </para>
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException"><paramref name="pattern"/> is not a valid pattern.</exception>
		/// <exception cref="InvalidOperationException"><c>AddSoapCoreUi</c> was not called on the service collection.</exception>
		/// <exception cref="OptionsValidationException">The configured <see cref="SoapUiOptions"/> are not valid.</exception>
		public static IEndpointConventionBuilder MapSoapCoreUi(this IEndpointRouteBuilder endpoints, string pattern = "/soap-ui")
		{
			if (endpoints is null)
			{
				throw new ArgumentNullException(nameof(endpoints));
			}

			if (pattern is null)
			{
				throw new ArgumentNullException(nameof(pattern));
			}

			if (!SoapUiPathRules.IsValidPath(pattern, SoapUiPathRules.MaxPathLength))
			{
				throw new ArgumentException(
					"The pattern must start with '/', must not end with '/', and may only contain segments of letters, digits, '.', '_', '~' and '-' (for example '/soap-ui').",
					nameof(pattern));
			}

			var services = endpoints.ServiceProvider;

			// The catalog constructor reads nothing, so a bad option cannot surface here with the wrong exception type.
			var catalog = services.GetService<SoapUiCatalog>();
			if (catalog is null)
			{
				throw new InvalidOperationException(
					"SoapCore.UI services are not registered. Call services.AddSoapCoreUi() in ConfigureServices before calling MapSoapCoreUi().");
			}

			// Read eagerly on purpose: it validates the options now and throws OptionsValidationException on a bad value.
			_ = services.GetRequiredService<IOptions<SoapUiOptions>>().Value;

			var assets = SoapUiAssetStore.Load();
			var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SoapUiRoutes>();
			var redirectSegment = pattern.Substring(pattern.LastIndexOf('/') + 1);

			// The explorer is same-origin by design. Cross-origin requests are refused by the handlers (see SoapUiRoutes), not with
			// DisableCors metadata, because any CORS metadata makes the endpoint middleware answer 500 when the host never calls UseCors().
			var group = endpoints.MapGroup(pattern);

			new SoapUiRoutes(catalog, assets, logger, redirectSegment).Map(group);

			return group;
		}
	}
}
