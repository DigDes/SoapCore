using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SoapCore.Extensibility;
using SoapCore.Meta;

#if NETCOREAPP3_0_OR_GREATER
using Microsoft.AspNetCore.Routing;
#endif

namespace SoapCore
{
	public static class SoapEndpointExtensions
	{
		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
		{
			return builder.UseSoapEndpoint<CustomMessage>(typeof(T), path, encoder, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, encoder, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
		{
			return builder.UseSoapEndpoint<CustomMessage>(type, path, encoder, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(type, options =>
			{
				options.Path = path;
				options.EncoderOptions = SoapEncoderOptions.ToArray(encoder);
				options.SoapSerializer = serializer;
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapModelBounder = soapModelBounder;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
				options.WsdlFileOptions = wsdlFileOptions;
			});
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, bool indentXml, bool omitXmlDeclaration)
		{
			return builder.UseSoapEndpoint(type, path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, binding, null, indentXml, omitXmlDeclaration);
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(type, path, new[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, binding, null, indentXml, omitXmlDeclaration);
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
		{
			return builder.UseSoapEndpoint(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder, null, indentXml, omitXmlDeclaration);
		}

		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, bool indentXml = true, bool omitXmlDeclaration = true)
		{
			return builder.UseSoapEndpoint<T, CustomMessage>(path, encoders, serializer, caseInsensitivePath, soapModelBounder, indentXml, omitXmlDeclaration);
		}

		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, bool indentXml = true, bool omitXmlDeclaration = true)
			where T_MESSAGE : CustomMessage, new()
		{
			return builder.UseSoapEndpoint<T_MESSAGE>(typeof(T), options =>
			{
				options.Path = path;
				options.EncoderOptions = encoders;
				options.SoapSerializer = serializer;
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapModelBounder = soapModelBounder;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
		{
			return builder.UseSoapEndpoint<CustomMessage>(type, path, encoderOptions, serializer, caseInsensitivePath, soapModelBounder, binding, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type type, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return UseSoapEndpoint<T_MESSAGE>(builder, type, options =>
			{
				options.Path = path;
				options.UseBasicAuthentication = binding.HasBasicAuth();
				options.EncoderOptions = encoderOptions ?? binding.ToEncoderOptions();
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapSerializer = serializer;
				options.SoapModelBounder = soapModelBounder;
				options.WsdlFileOptions = wsdlFileOptions;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type type, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
		{
			return builder.UseSoapEndpoint<CustomMessage>(type, path, binding, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		[Obsolete]
		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type type, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return UseSoapEndpoint<T_MESSAGE>(builder, type, options =>
			{
				options.Path = path;
				options.UseBasicAuthentication = binding.HasBasicAuth();
				options.EncoderOptions = binding.ToEncoderOptions();
				options.SoapSerializer = serializer;
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapModelBounder = soapModelBounder;
				options.WsdlFileOptions = wsdlFileOptions;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		public static IApplicationBuilder UseSoapEndpoint(this IApplicationBuilder builder, Type serviceType, Action<SoapCoreOptions> options)
		{
			return builder.UseSoapEndpoint<CustomMessage>(serviceType, options);
		}

		public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder, Action<SoapCoreOptions> options)
		{
			return builder.UseSoapEndpoint<T, CustomMessage>(options);
		}

		public static IApplicationBuilder UseSoapEndpoint<T, T_MESSAGE>(this IApplicationBuilder builder, Action<SoapCoreOptions> options)
			where T_MESSAGE : CustomMessage, new()
		{
			return UseSoapEndpoint<T_MESSAGE>(builder, typeof(T), options);
		}

		public static IApplicationBuilder UseSoapEndpoint<T_MESSAGE>(this IApplicationBuilder builder, Type serviceType, Action<SoapCoreOptions> options)
			where T_MESSAGE : CustomMessage, new()
		{
			var opt = new SoapCoreOptions();
			options(opt);

			var soapOptions = SoapOptions.FromSoapCoreOptions(opt, serviceType);

			return builder.UseMiddleware<SoapEndpointMiddleware<T_MESSAGE>>(soapOptions);
		}

#if NETCOREAPP3_0_OR_GREATER
		public static IEndpointConventionBuilder UseSoapEndpoint<T>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
		{
			return routes.UseSoapEndpoint<T, CustomMessage>(path, encoder, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, encoder, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
		{
			return routes.UseSoapEndpoint<CustomMessage>(type, path, encoder, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(type, options =>
			{
				options.Path = path;
				options.EncoderOptions = SoapEncoderOptions.ToArray(encoder);
				options.SoapSerializer = serializer;
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapModelBounder = soapModelBounder;
				options.WsdlFileOptions = wsdlFileOptions;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, bool indentXml, bool omitXmlDeclaration)
		{
			return routes.UseSoapEndpoint<CustomMessage>(type, path, encoder, serializer, caseInsensitivePath, soapModelBounder, binding, null, indentXml, omitXmlDeclaration);
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions encoder, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(type, options =>
			{
				options.Path = path;
				options.UseBasicAuthentication = binding.HasBasicAuth();
				options.EncoderOptions = SoapEncoderOptions.ToArray(encoder) ?? binding.ToEncoderOptions();
				options.SoapSerializer = serializer;
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapModelBounder = soapModelBounder;
				options.WsdlFileOptions = wsdlFileOptions;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint<T>(this IEndpointRouteBuilder routes, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
		{
			return routes.UseSoapEndpoint(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions: wsdlFileOptions, indentXml: indentXml, omitXmlDeclaration: omitXmlDeclaration);
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(typeof(T), path, binding, serializer, caseInsensitivePath, soapModelBounder, null, indentXml, omitXmlDeclaration);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
		{
			return routes.UseSoapEndpoint<T, CustomMessage>(path, encoders, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, bool indentXml = true, bool omitXmlDeclaration = true)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T, T_MESSAGE>(path, encoders, serializer, caseInsensitivePath, soapModelBounder, null, indentXml, omitXmlDeclaration);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer = SoapSerializer.DataContractSerializer, bool caseInsensitivePath = false, ISoapModelBounder soapModelBounder = null, WsdlFileOptions wsdlFileOptions = null, bool indentXml = true, bool omitXmlDeclaration = true)
		where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T, T_MESSAGE>(opt =>
			{
				opt.Path = path;
				opt.EncoderOptions = encoders;
				opt.SoapSerializer = serializer;
				opt.CaseInsensitivePath = caseInsensitivePath;
				opt.SoapModelBounder = soapModelBounder;
				opt.WsdlFileOptions = wsdlFileOptions;
				opt.IndentXml = indentXml;
				opt.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return UseSoapEndpoint<T_MESSAGE>(routes, type, options =>
			{
				options.Path = path;
				options.UseBasicAuthentication = binding.HasBasicAuth();
				options.EncoderOptions = encoders ?? binding.ToEncoderOptions();
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapSerializer = serializer;
				options.SoapModelBounder = soapModelBounder;
				options.WsdlFileOptions = wsdlFileOptions;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type type, string path, SoapEncoderOptions[] encoders, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
		{
			return UseSoapEndpoint<CustomMessage>(routes, type, path, encoders, serializer, caseInsensitivePath, soapModelBounder, binding, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type type, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
			where T_MESSAGE : CustomMessage, new()
		{
			return UseSoapEndpoint<T_MESSAGE>(routes, type, options =>
			{
				options.Path = path;
				options.UseBasicAuthentication = binding.HasBasicAuth();
				options.EncoderOptions = binding.ToEncoderOptions();
				options.SoapSerializer = serializer;
				options.CaseInsensitivePath = caseInsensitivePath;
				options.SoapModelBounder = soapModelBounder;
				options.WsdlFileOptions = wsdlFileOptions;
				options.IndentXml = indentXml;
				options.OmitXmlDeclaration = omitXmlDeclaration;
			});
		}

		[Obsolete]
		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type type, string path, Binding binding, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, WsdlFileOptions wsdlFileOptions, bool indentXml, bool omitXmlDeclaration)
		{
			return UseSoapEndpoint<CustomMessage>(routes, type, path, binding, serializer, caseInsensitivePath, soapModelBounder, wsdlFileOptions, indentXml, omitXmlDeclaration);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint(this IEndpointRouteBuilder routes, Type serviceType, Action<SoapCoreOptions> options)
		{
			return routes.UseSoapEndpoint<CustomMessage>(serviceType, options);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T, T_MESSAGE>(this IEndpointRouteBuilder routes, Action<SoapCoreOptions> options)
			where T_MESSAGE : CustomMessage, new()
		{
			return routes.UseSoapEndpoint<T_MESSAGE>(typeof(T), options);
		}

		public static IEndpointConventionBuilder UseSoapEndpoint<T_MESSAGE>(this IEndpointRouteBuilder routes, Type serviceType, Action<SoapCoreOptions> options)
			where T_MESSAGE : CustomMessage, new()
		{
			var opt = new SoapCoreOptions();
			options(opt);

			var soapOptions = SoapOptions.FromSoapCoreOptions(opt, serviceType);

			var pipeline = routes
				.CreateApplicationBuilder()
				.UseMiddleware<SoapEndpointMiddleware<T_MESSAGE>>(soapOptions)
				.Build();

			routes.Map(soapOptions.Path?.TrimEnd('/') + "/$metadata", pipeline);
			routes.Map(soapOptions.Path?.TrimEnd('/') + "/mex", pipeline);
			routes.Map(soapOptions.Path?.TrimEnd('/') + "/{methodName}", pipeline);

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

		[Obsolete]
		public static IServiceCollection AddSoapMessageInspector(this IServiceCollection serviceCollection, IMessageInspector messageInspector)
		{
			return serviceCollection.AddSoapMessageInspector(new ObsoleteMessageInspector(messageInspector));
		}

		public static IServiceCollection AddSoapMessageInspector<TService>(this IServiceCollection serviceCollection)
			where TService : class, IMessageInspector2
		{
			serviceCollection.AddScoped<IMessageInspector2, TService>();
			return serviceCollection;
		}

		public static IServiceCollection AddSoapMessageInspector(this IServiceCollection serviceCollection, IMessageInspector2 messageInspector)
		{
			serviceCollection.AddSingleton(messageInspector);
			return serviceCollection;
		}

		[Obsolete]
		public static IServiceCollection AddSoapMessageFilter(this IServiceCollection serviceCollection, IMessageFilter messageFilter)
		{
			return serviceCollection.AddSoapMessageFilter(new ObsoleteMessageFilter(messageFilter));
		}

		public static IServiceCollection AddSoapMessageFilter(this IServiceCollection serviceCollection, IAsyncMessageFilter messageFilter)
		{
			serviceCollection.AddSingleton(messageFilter);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapWsSecurityFilter(this IServiceCollection serviceCollection, string username, string password)
		{
			return serviceCollection.AddSoapMessageFilter(new WsMessageFilter(username, password));
		}

		public static IServiceCollection AddSoapModelBindingFilter(this IServiceCollection serviceCollection, IModelBindingFilter modelBindingFilter)
		{
			serviceCollection.AddSingleton(modelBindingFilter);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapServiceOperationTuner<TService>(this IServiceCollection serviceCollection)
			where TService : class, IServiceOperationTuner
		{
			serviceCollection.AddScoped<IServiceOperationTuner, TService>();
			return serviceCollection;
		}

		public static IServiceCollection AddSoapServiceOperationTuner(this IServiceCollection serviceCollection, IServiceOperationTuner serviceOperationTuner)
		{
			serviceCollection.AddSingleton(serviceOperationTuner);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapMessageProcessor(this IServiceCollection serviceCollection, ISoapMessageProcessor messageProcessor)
		{
			serviceCollection.AddSingleton(messageProcessor);
			return serviceCollection;
		}

		public static IServiceCollection AddSoapMessageProcessor(this IServiceCollection serviceCollection, Func<Message, HttpContext, Func<Message, Task<Message>>, Task<Message>> messageProcessor)
		{
			serviceCollection.AddSingleton<ISoapMessageProcessor>(new LambdaSoapMessageProcessor(messageProcessor));
			return serviceCollection;
		}

		public static IServiceCollection AddSoapMessageProcessor<TProcessor>(this IServiceCollection serviceCollection, ServiceLifetime lifetime = ServiceLifetime.Singleton)
			where TProcessor : class, ISoapMessageProcessor
		{
			serviceCollection.Add(new ServiceDescriptor(typeof(ISoapMessageProcessor), typeof(TProcessor), lifetime));
			return serviceCollection;
		}
	}
}
