using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace SoapCore.UI
{
	/// <summary>
	/// Service registration for the SoapCore.UI explorer.
	/// </summary>
	public static class SoapCoreUiServiceCollectionExtensions
	{
		private const string MinimumSoapCoreVersion = "1.2.1.17";

		/// <summary>
		/// Registers the services of the SoapCore.UI explorer, including the default SOAP endpoint registry.
		/// </summary>
		/// <param name="services">The service collection to add the services to.</param>
		/// <param name="configure">Optional delegate to configure <see cref="SoapUiOptions"/>.</param>
		/// <returns>The same service collection, for chaining.</returns>
		/// <remarks>
		/// Registers the default endpoint registry only if none is registered yet (TryAdd).
		/// A registry you add afterwards must use AddSingleton or Replace to take effect.
		/// Requires SoapCore 1.2.1.17 or later.
		/// Calling this method more than once is allowed: all configure delegates apply, in order.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
		/// <exception cref="InvalidOperationException">The loaded SoapCore does not contain <c>ISoapEndpointRegistry</c>.</exception>
		public static IServiceCollection AddSoapCoreUi(this IServiceCollection services, Action<SoapUiOptions>? configure = null)
		{
			if (services is null)
			{
				throw new ArgumentNullException(nameof(services));
			}

			try
			{
				RegisterRegistry(services);
			}
			catch (Exception ex) when (ex is MissingMemberException || ex is TypeLoadException)
			{
				throw new InvalidOperationException(
					"SoapCore.UI requires SoapCore " + MinimumSoapCoreVersion + " or later, the first release that contains ISoapEndpointRegistry. Update the SoapCore package.",
					ex);
			}

			services.AddLogging();
			services.TryAddSingleton<SoapUiDocumentationProvider>();
			services.TryAddSingleton<SoapUiCatalog>();
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SoapUiOptions>, SoapUiOptionsValidator>());

			var optionsBuilder = services.AddOptions<SoapUiOptions>();
			if (configure != null)
			{
				optionsBuilder.Configure(configure);
			}

			return services;
		}

		// A missing method surfaces when the JIT compiles this helper, which happens in the frame of the caller.
		// That is why the try/catch is in AddSoapCoreUi and the helper must not be inlined into it.
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void RegisterRegistry(IServiceCollection services)
		{
			services.AddSoapEndpointRegistry();
		}
	}
}
