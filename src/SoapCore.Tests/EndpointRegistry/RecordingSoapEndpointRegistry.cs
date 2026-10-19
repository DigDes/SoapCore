using System.Collections.Generic;
using System.Linq;
using SoapCore.Extensibility;

namespace SoapCore.Tests.EndpointRegistry
{
	public class RecordingSoapEndpointRegistry : ISoapEndpointRegistry
	{
		public List<SoapEndpointInfo> Registered { get; } = new List<SoapEndpointInfo>();

		public void Register(SoapEndpointInfo endpoint)
		{
			Registered.Add(endpoint);
		}

		public IReadOnlyList<SoapEndpointInfo> GetAll()
		{
			return Registered.ToArray();
		}
	}
}
