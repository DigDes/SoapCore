using System;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SoapCore.Extensibility;

#if ASPNET_30
using Microsoft.AspNetCore.Routing;
#endif

namespace SoapCore
{
	public static class SoapEndpointExtensions
	{
		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return builder.UseSoapEndpoint(typeof(T), path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
		{
			return builder.UseSoapEndpoint(type, path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(type, path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return builder.UseSoapEndpoint(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return builder.UseSoapEndpoint(typeof(T), path, encoders, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, encoders, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
		{
			return builder.UseSoapEndpoint<CustomMessage>(type, path, encoderOptions, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
			where T_MESSAGE : CustomMessage, new()
		{
			var options = new SoapOptions
			{
				Binding = binding,
				CaseInsensitivePath = caseInsensitivePath,
				EncoderOptions = encoderOptions,
				Path = path,
				ServiceType = type,
				SoapSerializer = serializer,
				SoapModelBounder = soapModelBounder
			};
			return builder.UseMiddleware<SoapEndpointMiddleware<T_MESSAGE>>(options);
		}

		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return builder.UseSoapEndpoint<CustomMessage>(type, path, binding, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type type, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			var elements = binding.CreateBindingElements().FindAll<MessageEncodingBindingElement>();
			var encoderOptions = new SoapEncoderOptions[elements.Count];

			for (var i = 0; i < encoderOptions.Length; i++)
			{
				var encoderOption = new SoapEncoderOptions
				{
					MessageVersion = elements[i].MessageVersion,
					WriteEncoding = Encoding.UTF8,
					ReaderQuotas = XmlDictionaryReaderQuotas.Max
				};

				if (elements[i] is TextMessageEncodingBindingElement textMessageEncodingBindingElement)
				{
					encoderOption.WriteEncoding = textMessageEncodingBindingElement.WriteEncoding;
					encoderOption.ReaderQuotas = textMessageEncodingBindingElement.ReaderQuotas;
				}

				encoderOptions[i] = encoderOption;
			}

			return builder.UseSoapEndpoint<T_MESSAGE>(type, path, encoderOptions, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, Action<SoapCoreOptions> options)
		{
			return builder.UseSoapEndpoint<T, CustomMessage>(options);
		}

		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, Action<SoapCoreOptions> options)
			where T_MESSAGE : CustomMessage, new()
		{
			var opt = new SoapCoreOptions();
			options(opt);

			// Generate encoders from Binding when they are not provided
			if (opt.EncoderOptions is null && opt.Binding != null)
			{
				var elements = opt.Binding.CreateBindingElements().FindAll<MessageEncodingBindingElement>();
				var encoderOptions = new SoapEncoderOptions[elements.Count];

				for (var i = 0; i < encoderOptions.Length; i++)
				{
					var encoderOption = new SoapEncoderOptions
					{
						MessageVersion = elements[i].MessageVersion,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					};

					if (elements[i] is TextMessageEncodingBindingElement textMessageEncodingBindingElement)
					{
						encoderOption.WriteEncoding = textMessageEncodingBindingElement.WriteEncoding;
						encoderOption.ReaderQuotas = textMessageEncodingBindingElement.ReaderQuotas;
					}

					encoderOptions[i] = encoderOption;
				}

				opt.EncoderOptions = encoderOptions;
			}

			var soapOptions = SoapOptions.FromSoapCoreOptions<T>(opt);

			return builder.UseMiddleware<SoapEndpointMiddleware<T_MESSAGE>>(soapOptions);
		}

#if ASPNET_30
		public static IEndpointConventionBuilder UseSoapEndpoint<T>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return routes.UseSoapEndpoint(typeof(T), path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
		{
			return routes.UseSoapEndpoint(type, path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(type, path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T>(this IEndpointRouteBuilder routes, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return routes.UseSoapEndpoint(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return routes.UseSoapEndpoint(typeof(T), path, encoders, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, encoders, serializer, caseInsensitivePath, soapModelBounder, null);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type type, string pattern, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
			where T_MESSAGE : CustomMessage, new()
		{
			var options = new SoapOptions
			{
				Binding = binding,
				CaseInsensitivePath = caseInsensitivePath,
				EncoderOptions = encoders,
				Path = pattern,
				ServiceType = type,
				SoapSerializer = serializer,
				SoapModelBounder = soapModelBounder
			};

			var pipeline = routes
				.CreateApplicationBuilder()
				.UseMiddleware<SoapEndpointMiddleware<T_MESSAGE>>(options)
				.Build();

			return routes.Map(pattern, pipeline)
				.WithDisplayName("SoapCore");
		}

		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type type, string pattern, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, Binding binding = null)
		{
			return UseSoapEndpoint<CustomMessage>(routes, type, pattern, encoders, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type type, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
			where T_MESSAGE : CustomMessage, new()
		{
			var elements = binding.CreateBindingElements().FindAll<MessageEncodingBindingElement>();
			var encoderOptions = new SoapEncoderOptions[elements.Count];

			for (var i = 0; i < encoderOptions.Length; i++)
			{
				var encoderOption = new SoapEncoderOptions
				{
					MessageVersion = elements[i].MessageVersion,
					WriteEncoding = Encoding.UTF8,
					ReaderQuotas = XmlDictionaryReaderQuotas.Max
				};

				if (elements[i] is TextMessageEncodingBindingElement textMessageEncodingBindingElement)
				{
					encoderOption.WriteEncoding = textMessageEncodingBindingElement.WriteEncoding;
					encoderOption.ReaderQuotas = textMessageEncodingBindingElement.ReaderQuotas;
				}

				encoderOptions[i] = encoderOption;
			}

			return routes.UseSoapEndpoint<T_MESSAGE>(type, path, encoderOptions, serializer, caseInsensitivePath, soapModelBounder, binding);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type type, string path, Binding binding, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null)
		{
			return UseSoapEndpoint<CustomMessage>(routes, type, path, binding, serializer, caseInsensitivePath, soapModelBounder);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, Action<SoapCoreOptions> options)
			where T_MESSAGE : CustomMessage, new()
		{
			var opt = new SoapCoreOptions();
			options(opt);

			// Generate encoders from Binding when they are not provided
			if (opt.EncoderOptions is null && opt.Binding != null)
			{
				var elements = opt.Binding.CreateBindingElements().FindAll<MessageEncodingBindingElement>();
				var encoderOptions = new SoapEncoderOptions[elements.Count];

				for (var i = 0; i < encoderOptions.Length; i++)
				{
					var encoderOption = new SoapEncoderOptions
					{
						MessageVersion = elements[i].MessageVersion,
						WriteEncoding = Encoding.UTF8,
						ReaderQuotas = XmlDictionaryReaderQuotas.Max
					};

					if (elements[i] is TextMessageEncodingBindingElement textMessageEncodingBindingElement)
					{
						encoderOption.WriteEncoding = textMessageEncodingBindingElement.WriteEncoding;
						encoderOption.ReaderQuotas = textMessageEncodingBindingElement.ReaderQuotas;
					}

					encoderOptions[i] = encoderOption;
				}

				opt.EncoderOptions = encoderOptions;
			}

			var soapOptions = SoapOptions.FromSoapCoreOptions<T>(opt);

			var pipeline = routes
				.CreateApplicationBuilder()
				.UseMiddleware<SoapEndpointMiddleware<T_MESSAGE>>(soapOptions)
				.Build();

			return routes.Map(soapOptions.Path, pipeline)
				.WithDisplayName("SoapCore");
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T>(this IEndpointRouteBuilder routes, Action<SoapCoreOptions> options)
		{
			return UseSoapEndpoint<T, CustomMessage>(routes, options);
		}
#endif

		public static IServiceCollection AddSoapCore(this IServiceCollection serviceCollection)
		{
			serviceCollection.TryAddSingleton<IOperationInvoker, DefaultOperationInvoker>();
			serviceCollection.TryAddSingleton<IFaultExceptionTransformer, DefaultFaultExceptionTransformer<CustomMessage>>();

			return serviceCollection;
		}

		public static IServiceCollection AddSoapCore<T_MESSAGE>(this IServiceCollection serviceCollection)
			where T_MESSAGE : CustomMessage, new()
		{
			serviceCollection.TryAddSingleton<IOperationInvoker, DefaultOperationInvoker>();
			serviceCollection.TryAddSingleton<IFaultExceptionTransformer, DefaultFaultExceptionTransformer<T_MESSAGE>>();

			return serviceCollection;
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
			serviceCollection.AddSingleton(messageInspector);
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
