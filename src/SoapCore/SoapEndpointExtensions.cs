using System;
using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SoapCore
{
	public static class SoapEndpointExtensions
	{
		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, MessageEncoder encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return builder.UseSoapEndpoint(typeof(T), path, encoder, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, MessageEncoder encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			if (soapModelBounder == null)
			{
				return builder.UseMiddleware<SoapEndpointMiddleware>(type, path, encoder, serializer, caseInsensitivePath);
			}

			return builder.UseMiddleware<SoapEndpointMiddleware>(type, path, encoder, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return builder.UseSoapEndpoint(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			var element = binding.CreateBindingElements().Find<MessageEncodingBindingElement>();
			var factory = element.CreateMessageEncoderFactory();
			var encoder = factory.Encoder;
			return builder.UseSoapEndpoint(type, path, encoder, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IServiceCollection AddSoapExceptionTransformer(this IServiceCollection serviceCollection, Func<Exception, string> transformer)
		{
			serviceCollection.TryAddSingleton(new ExceptionTransformer(transformer));
			return serviceCollection;
		}

		public static IServiceCollection AddSoapMessageInspector(this IServiceCollection serviceCollection, IMessageInspector messageInspector)
		{
			serviceCollection.TryAddSingleton(messageInspector);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapMessageInspector(this IServiceCollection serviceCollection, IMessageInspector2 messageInspector)
		{
			serviceCollection.TryAddSingleton(messageInspector);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapMessageFilter(this IServiceCollection serviceCollection, IMessageFilter messageFilter)
		{
			serviceCollection.TryAddSingleton(messageFilter);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapWsSecurityFilter(this IServiceCollection serviceCollection, string username, string password)
		{
			serviceCollection.AddSoapMessageFilter(new WsMessageFilter(username, password));
			return serviceCollection;
		}

		public static IServiceCollection AddSoapModelBindingFilter(this IServiceCollection serviceCollection, IModelBindingFilter modelBindingFilter)
		{
			serviceCollection.TryAddSingleton(modelBindingFilter);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapServiceOperationTuner(this IServiceCollection serviceCollection, IServiceOperationTuner serviceOperationTuner)
		{
			serviceCollection.TryAddSingleton(serviceOperationTuner);
			return serviceCollection;
		}
	}
}
