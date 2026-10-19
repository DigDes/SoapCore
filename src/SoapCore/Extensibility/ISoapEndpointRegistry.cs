using System.Collections.Generic;

namespace SoapCore.Extensibility
{
	/// <summary>
	/// Optional, SOAP-aware registry of the endpoints configured with <c>UseSoapEndpoint</c>.
	/// SoapCore only writes to it when an implementation is registered in the service collection,
	/// for example with <c>services.AddSoapEndpointRegistry()</c>.
	/// <para>
	/// Implementations are resolved from the root service provider while the request pipeline is configured,
	/// so they must be registered as singletons and be thread-safe.
	/// </para>
	/// <para>
	/// This interface is intentionally minimal and will not gain members. Future capabilities will be added
	/// as separate interfaces or as extension methods over <see cref="GetAll"/>.
	/// </para>
	/// </summary>
	public interface ISoapEndpointRegistry
	{
		/// <summary>
		/// Records a configured SOAP endpoint. SoapCore calls this once per <c>UseSoapEndpoint</c> call.
		/// </summary>
		/// <param name="endpoint">Snapshot of the endpoint configuration.</param>
		void Register(SoapEndpointInfo endpoint);

		/// <summary>
		/// Returns the registered endpoints in registration order.
		/// </summary>
		/// <returns>A read-only list of the registered endpoints.</returns>
		IReadOnlyList<SoapEndpointInfo> GetAll();
	}
}
