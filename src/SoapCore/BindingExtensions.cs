using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore
{
	internal static class BindingExtensions
	{
		public static bool HasBasicAuth(this Binding binding)
		{
			if (binding == null)
			{
				return false;
			}

			return binding.CreateBindingElements().Find<HttpTransportBindingElement>().AuthenticationScheme == AuthenticationSchemes.Basic;
		}
	}
}
