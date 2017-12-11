using System;
using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SoapCore
{
	public static class SoapEndpointExtensions
	{
		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, MessageEncoder encoder)
		{
			return builder.UseMiddleware<SoapEndpointMiddleware>(typeof(T), path, encoder);
		}

		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, Binding binding)
		{
			var element = binding.CreateBindingElements().Find<MessageEncodingBindingElement>();
			var factory = element.CreateMessageEncoderFactory();
			var encoder = factory.Encoder;
			return builder.UseMiddleware<SoapEndpointMiddleware>(typeof(T), path, encoder);
		}

		public static IServiceCollection AddSoapExceptionTransformer(this IServiceCollection serviceCollection, Func<Exception, string> transformer)
		{
			serviceCollection.TryAddSingleton(new ExceptionTransformer(transformer));
			return serviceCollection;
		}
	}
}
