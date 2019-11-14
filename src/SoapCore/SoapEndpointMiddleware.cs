using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoapCore.Extensibility;
using SoapCore.MessageEncoder;
using SoapCore.Meta;
using SoapCore.ServiceModel;

namespace SoapCore
{
	public class SoapEndpointMiddleware
	{
		private readonly ILogger<SoapEndpointMiddleware> _logger;
		private readonly RequestDelegate _next;
		private readonly ServiceDescription _service;
		private readonly string _endpointPath;
		private readonly SoapSerializer _serializer;
		private readonly Binding _binding;
		private readonly StringComparison _pathComparisonStrategy;
		private readonly ISoapModelBounder _soapModelBounder;
		private readonly bool _httpGetEnabled;
		private readonly bool _httpsGetEnabled;
		private readonly SoapMessageEncoder[] _messageEncoders;
		private readonly SerializerHelper _serializerHelper;

		[Obsolete]
		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware> logger, RequestDelegate next, Type serviceType, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, bool httpGetEnabled, bool httpsGetEnabled)
		{
			_logger = logger;
			_next = next;
			_endpointPath = path;
			_serializer = serializer;
			_serializerHelper = new SerializerHelper(_serializer);
			_pathComparisonStrategy = caseInsensitivePath ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_service = new ServiceDescription(serviceType);
			_soapModelBounder = soapModelBounder;
			_binding = binding;
			_httpGetEnabled = httpGetEnabled;
			_httpsGetEnabled = httpsGetEnabled;

			_messageEncoders = new SoapMessageEncoder[encoderOptions.Length];

			for (var i = 0; i < encoderOptions.Length; i++)
			{
				_messageEncoders[i] = new SoapMessageEncoder(encoderOptions[i].MessageVersion, encoderOptions[i].WriteEncoding, encoderOptions[i].ReaderQuotas);
			}
		}

		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware> logger, RequestDelegate next, SoapOptions options)
		{
			_logger = logger;
			_next = next;
			_endpointPath = options.Path;
			_serializer = options.SoapSerializer;
			_serializerHelper = new SerializerHelper(_serializer);
			_pathComparisonStrategy = options.CaseInsensitivePath ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_service = new ServiceDescription(options.ServiceType);
			_soapModelBounder = options.SoapModelBounder;
			_binding = options.Binding;
			_httpGetEnabled = options.HttpGetEnabled;
			_httpsGetEnabled = options.HttpsGetEnabled;

			_messageEncoders = new SoapMessageEncoder[options.EncoderOptions.Length];

			for (var i = 0; i < options.EncoderOptions.Length; i++)
			{
				_messageEncoders[i] = new SoapMessageEncoder(options.EncoderOptions[i].MessageVersion, options.EncoderOptions[i].WriteEncoding, options.EncoderOptions[i].ReaderQuotas);
			}
		}

		public async Task Invoke(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			httpContext.Request.EnableBuffering();

			var trailPathTuner = serviceProvider.GetServices<TrailingServicePathTuner>().FirstOrDefault();

			trailPathTuner?.ConvertPath(httpContext);

			if (httpContext.Request.Path.Equals(_endpointPath, _pathComparisonStrategy))
			{
				if (httpContext.Request.Method?.ToLower() == "get")
				{
					// If GET is not enabled, either for HTTP or HTTPS, return a 403 instead of the WSDL
					if ((httpContext.Request.IsHttps && !_httpsGetEnabled) || (!httpContext.Request.IsHttps && !_httpGetEnabled))
					{
						httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
						return;
					}
				}

				try
				{
					_logger.LogDebug($"Received SOAP Request for {httpContext.Request.Path} ({httpContext.Request.ContentLength ?? 0} bytes)");

					if (httpContext.Request.Query.ContainsKey("wsdl") && httpContext.Request.Method?.ToLower() == "get")
					{
						await ProcessMeta(httpContext);
					}
					else
					{
						await ProcessOperation(httpContext, serviceProvider);
					}
				}
				catch (Exception ex)
				{
					_logger.LogCritical(ex, $"An error occurred when trying to service a request on SOAP endpoint: {httpContext.Request.Path}");

					// Let's pass this up the middleware chain after we have logged this issue
					// and signaled the critical of it
					throw;
				}
			}
			else
			{
				await _next(httpContext);
			}
		}

		private async Task ProcessMeta(HttpContext httpContext)
		{
			var baseUrl = httpContext.Request.Scheme + "://" + httpContext.Request.Host + httpContext.Request.PathBase + httpContext.Request.Path;

			var bodyWriter = _serializer == SoapSerializer.XmlSerializer ? new MetaBodyWriter(_service, baseUrl, _binding) : (BodyWriter)new MetaWCFBodyWriter(_service, baseUrl, _binding);

			var responseMessage = Message.CreateMessage(_messageEncoders[0].MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, _service, _binding);

			httpContext.Response.ContentType = _messageEncoders[0].ContentType;

#if ASPNET_21
			await _messageEncoders[0].WriteMessageAsync(responseMessage, httpContext.Response.Body);
#endif

#if ASPNET_30
			await _messageEncoders[0].WriteMessageAsync(responseMessage, httpContext.Response.BodyWriter);
#endif
		}

		private async Task ProcessOperation(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			Message responseMessage;

			//Reload the body to ensure we have the full message
			var memoryStream = new MemoryStream((int)httpContext.Request.ContentLength.GetValueOrDefault(1024));
			await httpContext.Request.Body.CopyToAsync(memoryStream).ConfigureAwait(false);
			memoryStream.Seek(0, SeekOrigin.Begin);
			httpContext.Request.Body = memoryStream;

			//Return metadata if no request, provided this is a GET request
			if (httpContext.Request.Body.Length == 0 && httpContext.Request.Method?.ToLower() == "get")
			{
				await ProcessMeta(httpContext);
				return;
			}

			// Get the encoder based on Content Type
			var messageEncoder = _messageEncoders[0];

			foreach (var encoder in _messageEncoders)
			{
				if (encoder.IsContentTypeSupported(httpContext.Request.ContentType))
				{
					messageEncoder = encoder;
					break;
				}
			}

			//Get the message
#if ASPNET_30
			var requestMessage = await messageEncoder.ReadMessageAsync(httpContext.Request.BodyReader, 0x10000, httpContext.Request.ContentType);
#endif

#if ASPNET_21
			var requestMessage = await messageEncoder.ReadMessageAsync(httpContext.Request.Body, 0x10000, httpContext.Request.ContentType);
#endif
			var messageFilters = serviceProvider.GetServices<IMessageFilter>().ToArray();
			var asyncMessageFilters = serviceProvider.GetServices<IAsyncMessageFilter>().ToArray();

			//Execute request message filters
			try
			{
				foreach (var messageFilter in messageFilters)
				{
					messageFilter.OnRequestExecuting(requestMessage);
				}

				foreach (var messageFilter in asyncMessageFilters)
				{
					await messageFilter.OnRequestExecuting(requestMessage);
				}
			}
			catch (Exception ex)
			{
				await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				return;
			}

			var messageInspector = serviceProvider.GetService<IMessageInspector>();
			object correlationObject;

			try
			{
				correlationObject = messageInspector?.AfterReceiveRequest(ref requestMessage);
			}
			catch (Exception ex)
			{
				await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				return;
			}

			var messageInspector2s = serviceProvider.GetServices<IMessageInspector2>();
#pragma warning disable SA1009 // StyleCop has not yet been updated to support tuples
			var correlationObjects2 = default(List<(IMessageInspector2 inspector, object correlationObject)>);
#pragma warning restore SA1009

			try
			{
#pragma warning disable SA1008 // StyleCop has not yet been updated to support tuples
				correlationObjects2 = messageInspector2s.Select(mi => (inspector: mi, correlationObject: mi.AfterReceiveRequest(ref requestMessage, _service))).ToList();
#pragma warning restore SA1008
			}
			catch (Exception ex)
			{
				await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				return;
			}

			// for getting soapaction and parameters in body
			// GetReaderAtBodyContents must not be called twice in one request
			using (var reader = requestMessage.GetReaderAtBodyContents())
			{
				var soapAction = HeadersHelper.GetSoapAction(httpContext, requestMessage, reader);
				requestMessage.Headers.Action = soapAction;
				var operation = _service.Operations.FirstOrDefault(o => o.SoapAction.Equals(soapAction, StringComparison.Ordinal) || o.Name.Equals(soapAction, StringComparison.Ordinal));
				if (operation == null)
				{
					throw new InvalidOperationException($"No operation found for specified action: {requestMessage.Headers.Action}");
				}

				_logger.LogInformation($"Request for operation {operation.Contract.Name}.{operation.Name} received");

				try
				{
					//Create an instance of the service class
					var serviceInstance = serviceProvider.GetRequiredService(_service.ServiceType);

					SetMessageHeadersToProperty(requestMessage, serviceInstance);

					// Get operation arguments from message
					var arguments = GetRequestArguments(requestMessage, reader, operation, httpContext);

					ExecuteFiltersAndTune(httpContext, serviceProvider, operation, arguments, serviceInstance);

					var invoker = serviceProvider.GetService<IOperationInvoker>() ?? new DefaultOperationInvoker();
					var responseObject = await invoker.InvokeAsync(operation.DispatchMethod, serviceInstance, arguments);

					var resultOutDictionary = new Dictionary<string, object>();
					foreach (var parameterInfo in operation.OutParameters)
					{
						resultOutDictionary[parameterInfo.Name] = arguments[parameterInfo.Index];
					}

					responseMessage = CreateResponseMessage(operation, responseObject, resultOutDictionary, soapAction, requestMessage);

					httpContext.Response.ContentType = httpContext.Request.ContentType;
					httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;

					correlationObjects2.ForEach(mi => mi.inspector.BeforeSendReply(ref responseMessage, _service, mi.correlationObject));

					messageInspector?.BeforeSendReply(ref responseMessage, correlationObject);

					SetHttpResponse(httpContext, responseMessage);

#if ASPNET_30
					await _messageEncoders[0].WriteMessageAsync(responseMessage, httpContext.Response.BodyWriter);
#endif

#if ASPNET_21
					await _messageEncoders[0].WriteMessageAsync(responseMessage, httpContext.Response.Body);
#endif
				}
				catch (Exception exception)
				{
					if (exception is TargetInvocationException targetInvocationException)
					{
						exception = targetInvocationException.InnerException;
					}

					_logger.LogWarning(0, exception, exception?.Message);
					responseMessage = await WriteErrorResponseMessage(exception, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				}
			}

			// Execute response message filters
			try
			{
				foreach (var messageFilter in messageFilters)
				{
					messageFilter.OnResponseExecuting(responseMessage);
				}

				foreach (var messageFilter in asyncMessageFilters.Reverse())
				{
					await messageFilter.OnResponseExecuting(responseMessage);
				}
			}
			catch (Exception ex)
			{
				responseMessage = await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
			}
		}

		private Message CreateResponseMessage(OperationDescription operation, object responseObject, Dictionary<string, object> resultOutDictionary, string soapAction, Message requestMessage)
		{
			Message responseMessage;

			// Create response message
			var bodyWriter = new ServiceBodyWriter(_serializer, operation, responseObject, resultOutDictionary);

			if (_messageEncoders[0].MessageVersion.Addressing == AddressingVersion.WSAddressing10)
			{
				responseMessage = Message.CreateMessage(_messageEncoders[0].MessageVersion, soapAction, bodyWriter);
				responseMessage = new CustomMessage(responseMessage);

				responseMessage.Headers.Action = operation.ReplyAction;
				responseMessage.Headers.RelatesTo = requestMessage.Headers.MessageId;
				responseMessage.Headers.To = requestMessage.Headers.ReplyTo?.Uri;
			}
			else
			{
				responseMessage = Message.CreateMessage(_messageEncoders[0].MessageVersion, null, bodyWriter);
				responseMessage = new CustomMessage(responseMessage);

				if (responseObject != null)
				{
					var messageHeaderMembers = responseObject.GetType().GetMembersWithAttribute<MessageHeaderAttribute>();
					foreach (var messageHeaderMember in messageHeaderMembers)
					{
						var messageHeaderAttribute = messageHeaderMember.GetCustomAttribute<MessageHeaderAttribute>();
						responseMessage.Headers.Add(MessageHeader.CreateHeader(messageHeaderAttribute.Name ?? messageHeaderMember.Name, operation.Contract.Namespace, messageHeaderMember.GetPropertyOrFieldValue(responseObject)));
					}
				}
			}

			return responseMessage;
		}

		private void ExecuteFiltersAndTune(HttpContext httpContext, IServiceProvider serviceProvider, OperationDescription operation, object[] arguments, object serviceInstance)
		{
			// Execute model binding filters
			object modelBindingOutput = null;
			foreach (var modelBindingFilter in serviceProvider.GetServices<IModelBindingFilter>())
			{
				foreach (var modelType in modelBindingFilter.ModelTypes)
				{
					foreach (var parameterInfo in operation.InParameters)
					{
						var arg = arguments[parameterInfo.Index];
						if (arg != null && arg.GetType() == modelType)
						{
							modelBindingFilter.OnModelBound(arg, serviceProvider, out modelBindingOutput);
						}
					}
				}
			}

			// Execute Mvc ActionFilters
			foreach (var actionFilterAttr in operation.DispatchMethod.CustomAttributes.Where(a => a.AttributeType.Name == "ServiceFilterAttribute"))
			{
				var actionFilter = serviceProvider.GetService(actionFilterAttr.ConstructorArguments[0].Value as Type);
				actionFilter.GetType().GetMethod("OnSoapActionExecuting")?.Invoke(actionFilter, new[] { operation.Name, arguments, httpContext, modelBindingOutput });
			}

			// Invoke OnModelBound
			_soapModelBounder?.OnModelBound(operation.DispatchMethod, arguments);

			// Tune service instance for operation call
			var serviceOperationTuners = serviceProvider.GetServices<IServiceOperationTuner>();
			foreach (var operationTuner in serviceOperationTuners)
			{
				operationTuner.Tune(httpContext, serviceInstance, operation);
			}
		}

		private void SetMessageHeadersToProperty(Message requestMessage, object serviceInstance)
		{
			var headerProperty = _service.ServiceType.GetProperty("MessageHeaders");
			if (headerProperty != null && headerProperty.PropertyType == requestMessage.Headers.GetType())
			{
				headerProperty.SetValue(serviceInstance, requestMessage.Headers);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private object[] GetRequestArguments(Message requestMessage, System.Xml.XmlDictionaryReader xmlReader, OperationDescription operation, HttpContext httpContext)
		{
			var arguments = new object[operation.AllParameters.Length];

			// if any ordering issues, possible to rewrite like:
			/*while (!xmlReader.EOF)
			{
				var parameterInfo = operation.InParameters.FirstOrDefault(p => p.Name == xmlReader.LocalName && p.Namespace == xmlReader.NamespaceURI);
				if (parameterInfo == null)
				{
					xmlReader.Skip();
					continue;
				}
				var parameterName = parameterInfo.Name;
				var parameterNs = parameterInfo.Namespace;
				...
			}*/

			// Find the element for the operation's data
			if (!operation.IsMessageContractRequest)
			{
				xmlReader.ReadStartElement(operation.Name, operation.Contract.Namespace);

				foreach (var parameterInfo in operation.InParameters)
				{
					var parameterType = parameterInfo.Parameter.ParameterType;

					if (parameterType == typeof(HttpContext))
					{
						arguments[parameterInfo.Index] = httpContext;
					}
					else
					{
						arguments[parameterInfo.Index] = _serializerHelper.DeserializeInputParameter(xmlReader, parameterType, parameterInfo.Name, operation.Contract.Namespace, parameterInfo);
					}
				}
			}
			else
			{
				// MessageContracts are constrained to having one "InParameter". We can do special logic on
				// for this
				Debug.Assert(operation.InParameters.Length == 1, "MessageContracts are constrained to having one 'InParameter'");

				var parameterInfo = operation.InParameters[0];
				var parameterType = parameterInfo.Parameter.ParameterType;

				var messageContractAttribute = parameterType.GetCustomAttribute<MessageContractAttribute>();

				Debug.Assert(messageContractAttribute != null, "operation.IsMessageContractRequest should be false if this is null");

				var @namespace = parameterInfo.Namespace ?? operation.Contract.Namespace;

				if (messageContractAttribute.IsWrapped && !parameterType.GetMembersWithAttribute<MessageHeaderAttribute>().Any())
				{
					// It's wrapped so we treat it like normal!
					arguments[parameterInfo.Index] = _serializerHelper.DeserializeInputParameter(xmlReader, parameterInfo.Parameter.ParameterType, parameterInfo.Name, @namespace, parameterInfo);
				}
				else
				{
					var messageHeadersMembers = parameterType.GetPropertyOrFieldMembers().Where(x => x.GetCustomAttribute<MessageHeaderAttribute>() != null).ToArray();

					var wrapperObject = Activator.CreateInstance(parameterInfo.Parameter.ParameterType);

					for (var i = 0; i < requestMessage.Headers.Count; i++)
					{
						var header = requestMessage.Headers[i];
						var member = messageHeadersMembers.FirstOrDefault(x => x.Name == header.Name);

						if (member != null)
						{
							var messageHeaderAttribute = member.GetCustomAttribute<MessageHeaderAttribute>();
							var reader = requestMessage.Headers.GetReaderAtHeader(i);

							var value = _serializerHelper.DeserializeInputParameter(reader, member.GetPropertyOrFieldType(), messageHeaderAttribute.Name ?? member.Name, messageHeaderAttribute.Namespace ?? @namespace);

							member.SetValueToPropertyOrField(wrapperObject, value);
						}
					}

					// This object isn't a wrapper element, so we will hunt for the nested message body
					// member inside of it
					var messageBodyMembers = parameterType.GetPropertyOrFieldMembers().Where(x => x.GetCustomAttribute<MessageBodyMemberAttribute>() != null).Select(mi => new
					{
						Member = mi,
						MessageBodyMemberAttribute = mi.GetCustomAttribute<MessageBodyMemberAttribute>()
					}).OrderBy(x => x.MessageBodyMemberAttribute.Order);

					if (messageContractAttribute.IsWrapped)
					{
						xmlReader.Read();
					}

					foreach (var messageBodyMember in messageBodyMembers)
					{
						var messageBodyMemberAttribute = messageBodyMember.MessageBodyMemberAttribute;
						var messageBodyMemberInfo = messageBodyMember.Member;

						var innerParameterName = messageBodyMemberAttribute.Name ?? messageBodyMemberInfo.Name;
						var innerParameterNs = messageBodyMemberAttribute.Namespace ?? @namespace;
						var innerParameterType = messageBodyMemberInfo.GetPropertyOrFieldType();

						//xmlReader.MoveToStartElement(innerParameterName, innerParameterNs);
						var innerParameter = _serializerHelper.DeserializeInputParameter(xmlReader, innerParameterType, innerParameterName, innerParameterNs, parameterInfo);

						messageBodyMemberInfo.SetValueToPropertyOrField(wrapperObject, innerParameter);
					}

					arguments[parameterInfo.Index] = wrapperObject;
				}
			}

			foreach (var parameterInfo in operation.OutParameters)
			{
				if (arguments[parameterInfo.Index] != null)
				{
					// do not overwrite input ref parameters
					continue;
				}

				if (parameterInfo.Parameter.ParameterType.Name == "Guid&")
				{
					arguments[parameterInfo.Index] = Guid.Empty;
				}
				else if (parameterInfo.Parameter.ParameterType.Name == "String&" || parameterInfo.Parameter.ParameterType.GetElementType().IsArray)
				{
					arguments[parameterInfo.Index] = null;
				}
				else
				{
					var type = parameterInfo.Parameter.ParameterType.GetElementType();
					arguments[parameterInfo.Index] = Activator.CreateInstance(type);
				}
			}

			return arguments;
		}

		/// <summary>
		/// Helper message to write an error response message in case of an exception.
		/// </summary>
		/// <param name="exception">
		/// The exception that caused the failure.
		/// </param>
		/// <param name="statusCode">
		/// The HTTP status code that shall be returned to the caller.
		/// </param>
		/// <param name="serviceProvider">
		/// The DI container.
		/// </param>
		/// <param name="requestMessage">
		/// The Message for the incoming request
		/// </param>
		/// <param name="httpContext">
		/// The HTTP context that received the response message.
		/// </param>
		/// <returns>
		/// Returns the constructed message (which is implicitly written to the response
		/// and therefore must not be handled by the caller).
		/// </returns>
		private async Task<Message> WriteErrorResponseMessage(
			Exception exception,
			int statusCode,
			IServiceProvider serviceProvider,
			Message requestMessage,
			HttpContext httpContext)
		{
			var faultExceptionTransformer = serviceProvider.GetRequiredService<IFaultExceptionTransformer>();
			var faultMessage = faultExceptionTransformer.ProvideFault(exception, _messageEncoders[0].MessageVersion);

			httpContext.Response.ContentType = httpContext.Request.ContentType;
			httpContext.Response.Headers["SOAPAction"] = faultMessage.Headers.Action;
			httpContext.Response.StatusCode = statusCode;

			SetHttpResponse(httpContext, faultMessage);

			if (_messageEncoders[0].MessageVersion.Addressing == AddressingVersion.WSAddressing10)
			{
				// TODO: Some additional work needs to be done in order to support setting the action. Simply setting it to
				// "http://www.w3.org/2005/08/addressing/fault" will cause the WCF Client to not be able to figure out the type
				faultMessage.Headers.RelatesTo = requestMessage.Headers.MessageId;
				faultMessage.Headers.To = requestMessage.Headers.ReplyTo?.Uri;
			}

#if ASPNET_30
			await _messageEncoders[0].WriteMessageAsync(faultMessage, httpContext.Response.BodyWriter);
#endif

#if ASPNET_21
			await _messageEncoders[0].WriteMessageAsync(faultMessage, httpContext.Response.Body);
#endif

			return faultMessage;
		}

		private void SetHttpResponse(HttpContext httpContext, Message message)
		{
			if (!message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out var value)
#pragma warning disable SA1119 // StatementMustNotUseUnnecessaryParenthesis
				|| !(value is HttpResponseMessageProperty httpProperty))
#pragma warning restore SA1119 // StatementMustNotUseUnnecessaryParenthesis
			{
				return;
			}

			httpContext.Response.StatusCode = (int)httpProperty.StatusCode;

			var feature = httpContext.Features.Get<IHttpResponseFeature>();
			if (feature != null && !string.IsNullOrEmpty(httpProperty.StatusDescription))
			{
				feature.ReasonPhrase = httpProperty.StatusDescription;
			}

			foreach (string key in httpProperty.Headers.Keys)
			{
				httpContext.Response.Headers.Add(key, httpProperty.Headers.GetValues(key));
			}
		}
	}
}
