using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using SoapCore.Extensibility;

namespace SoapCore
{
	/// <summary>
	/// Default thread-safe <see cref="ISoapEndpointRegistry"/> registered by <c>AddSoapEndpointRegistry()</c>.
	/// Keeps every registration in order, never removes duplicates, and returns lock-free point-in-time snapshots.
	/// </summary>
	internal class DefaultSoapEndpointRegistry : ISoapEndpointRegistry
	{
		private readonly object _lock = new object();
		private ReadOnlyCollection<SoapEndpointInfo> _endpoints = Array.AsReadOnly(Array.Empty<SoapEndpointInfo>());

		public void Register(SoapEndpointInfo endpoint)
		{
			if (endpoint == null)
			{
				throw new ArgumentNullException(nameof(endpoint));
			}

			lock (_lock)
			{
				var endpoints = new SoapEndpointInfo[_endpoints.Count + 1];
				_endpoints.CopyTo(endpoints, 0);
				endpoints[endpoints.Length - 1] = endpoint;
				Volatile.Write(ref _endpoints, Array.AsReadOnly(endpoints));
			}
		}

		public IReadOnlyList<SoapEndpointInfo> GetAll()
		{
			return Volatile.Read(ref _endpoints);
		}
	}
}
