using System.Net;
using System.ServiceModel.Channels;

namespace SoapCore
{
	internal static class BindingExtensions
	{
		public static bool HasBasicAuth(this Binding binding)
		{
			var transportBindingElement = binding?.CreateBindingElements().Find<HttpTransportBindingElement>();

			if (transportBindingElement != null)
			{
				return transportBindingElement.AuthenticationScheme == AuthenticationSchemes.Basic;
			}

			return false;
		}
	}
}
