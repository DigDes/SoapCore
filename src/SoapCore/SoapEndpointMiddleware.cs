using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoapCore.MessageEncoder;

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

		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware> logger, RequestDelegate next, Type serviceType, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, bool httpGetEnabled, bool httpsGetEnabled)
		{
			_logger = logger;
			_next = next;
			_endpointPath = path;
			_serializer = serializer;
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

			if (trailPathTuner != null)
			{
				trailPathTuner.ConvertPath(httpContext);
			}

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
					// and signaled the criticality of it
					throw;
				}
			}
			else
			{
				await _next(httpContext);
			}
		}

		private async Task<Message> ProcessMeta(HttpContext httpContext)
		{
			string baseUrl = httpContext.Request.Scheme + "://" + httpContext.Request.Host + httpContext.Request.PathBase + httpContext.Request.Path;

			BodyWriter bodyWriter;
			switch (_serializer)
			{
				case SoapSerializer.XmlSerializer:
					bodyWriter = new MetaBodyWriter(_service, baseUrl, _binding);
					break;

				case SoapSerializer.DataContractSerializer:
					bodyWriter = new MetaWCFBodyWriter(_service, baseUrl, _binding);
					break;

				case SoapSerializer.XmlWcfSerializer:
					bodyWriter = new MetaWcfXmlBodyWriter(_service, baseUrl, _binding);
					break;

				default:
					throw new NotImplementedException();
			}

			var responseMessage = Message.CreateMessage(_messageEncoders[0].MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, _service, _binding);

			httpContext.Response.ContentType = _messageEncoders[0].ContentType;

#if ASPNET_21
			await _messageEncoders[0].WriteMessageAsync(responseMessage, httpContext.Response.Body);
#endif

#if ASPNET_30
			await _messageEncoders[0].WriteMessageAsync(responseMessage, httpContext.Response.BodyWriter);
#endif

			return responseMessage;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private string GetSoapAction(HttpContext httpContext, Message requestMessage, System.Xml.XmlDictionaryReader reader)
		{
			var soapAction = httpContext.Request.Headers["SOAPAction"].FirstOrDefault();
			if (soapAction == "\"\"")
			{
				soapAction = string.Empty;
			}

			if (string.IsNullOrEmpty(soapAction))
			{
				foreach (var headerItem in httpContext.Request.Headers["Content-Type"])
				{
					// I want to avoid allocation as possible as I can(I hope to use Span<T> or Utf8String)
					// soap1.2: action name is in Content-Type(like 'action="[action url]"') or body
					int i = 0;

					// skip whitespace
					while (i < headerItem.Length && headerItem[i] == ' ')
					{
						i++;
					}

					if (headerItem.Length - i < 6)
					{
						continue;
					}

					// find 'action'
					if (headerItem[i + 0] == 'a'
						&& headerItem[i + 1] == 'c'
						&& headerItem[i + 2] == 't'
						&& headerItem[i + 3] == 'i'
						&& headerItem[i + 4] == 'o'
						&& headerItem[i + 5] == 'n')
					{
						i += 6;

						// skip white space
						while (i < headerItem.Length && headerItem[i] == ' ')
						{
							i++;
						}

						if (headerItem[i] == '=')
						{
							i++;

							// skip whitespace
							while (i < headerItem.Length && headerItem[i] == ' ')
							{
								i++;
							}

							// action value should be surrounded by '"'
							if (headerItem[i] == '"')
							{
								i++;
								int offset = i;
								while (i < headerItem.Length && headerItem[i] != '"')
								{
									i++;
								}

								if (i < headerItem.Length && headerItem[i] == '"')
								{
									var charray = headerItem.ToCharArray();
									soapAction = new string(charray, offset, i - offset);
									break;
								}
							}
						}
					}
				}

				if (string.IsNullOrEmpty(soapAction))
				{
					soapAction = reader.LocalName;
				}
			}

			if (soapAction.Contains('/'))
			{
				// soapAction may be a path. Therefore must take the action from the path provided.
				soapAction = soapAction.Split('/').Last();
			}

			if (!string.IsNullOrEmpty(soapAction))
			{
				// soapAction may have '"' in some cases.
				soapAction = soapAction.Trim('"');
			}

			return soapAction;
		}

		private async Task<Message> ProcessOperation(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			Message responseMessage;

			//Reload the body to ensure we have the full message
			var mstm = new MemoryStream((int)httpContext.Request.ContentLength.GetValueOrDefault(1024));
			await httpContext.Request.Body.CopyToAsync(mstm).ConfigureAwait(false);
			mstm.Seek(0, SeekOrigin.Begin);
			httpContext.Request.Body = mstm;

			//Return metadata if no request, provided this is a GET request
			if (httpContext.Request.Body.Length == 0 && httpContext.Request.Method?.ToLower() == "get")
			{
				return await ProcessMeta(httpContext);
			}

			// Get the encoder based on Content Type
			var messageEncoder = _messageEncoders[0];

			for (var i = 0; i < _messageEncoders.Length; i++)
			{
				if (_messageEncoders[i].IsContentTypeSupported(httpContext.Request.ContentType))
				{
					messageEncoder = _messageEncoders[i];
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

			// Get MessageFilters, ModelBindingFilters
			var messageFilters = serviceProvider.GetServices<IMessageFilter>();
			var asyncMessageFilters = serviceProvider.GetServices<IAsyncMessageFilter>();
			var modelBindingFilters = serviceProvider.GetServices<IModelBindingFilter>();

			// Execute request message filters
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
				responseMessage = await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				return responseMessage;
			}

			var messageInspector = serviceProvider.GetService<IMessageInspector>();
			var correlationObject = default(object);

			try
			{
				correlationObject = messageInspector?.AfterReceiveRequest(ref requestMessage);
			}
			catch (Exception ex)
			{
				responseMessage = await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				return responseMessage;
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
				responseMessage = await WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, requestMessage, httpContext);
				return responseMessage;
			}

			// for getting soapaction and parameters in body
			// GetReaderAtBodyContents must not be called twice in one request
			using (var reader = requestMessage.GetReaderAtBodyContents())
			{
				var soapAction = GetSoapAction(httpContext, requestMessage, reader);
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

					var headerProperty = _service.ServiceType.GetProperty("MessageHeaders");
					if (headerProperty != null && headerProperty.PropertyType == requestMessage.Headers.GetType())
					{
						headerProperty.SetValue(serviceInstance, requestMessage.Headers);
					}

					// Get operation arguments from message
					var arguments = GetRequestArguments(requestMessage, reader, operation, httpContext);

					// Execute model binding filters
					object modelBindingOutput = null;
					foreach (var modelBindingFilter in modelBindingFilters)
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
						actionFilter.GetType().GetMethod("OnSoapActionExecuting").Invoke(actionFilter, new object[] { operation.Name, arguments, httpContext, modelBindingOutput });
					}

					// Invoke OnModelBound
					_soapModelBounder?.OnModelBound(operation.DispatchMethod, arguments);

					// Tune service instance for operation call
					var serviceOperationTuners = serviceProvider.GetServices<IServiceOperationTuner>();
					foreach (var operationTuner in serviceOperationTuners)
					{
						operationTuner.Tune(httpContext, serviceInstance, operation);
					}

					var invoker = serviceProvider.GetService<IOperationInvoker>() ?? new DefaultOperationInvoker();
					var responseObject = await invoker.InvokeAsync(operation.DispatchMethod, serviceInstance, arguments);

					var resultOutDictionary = new Dictionary<string, object>();
					foreach (var parameterInfo in operation.OutParameters)
					{
						resultOutDictionary[parameterInfo.Name] = arguments[parameterInfo.Index];
					}

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
					}

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

					_logger.LogWarning(0, exception, exception.Message);
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
				return responseMessage;
			}

			return responseMessage;
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
						arguments[parameterInfo.Index] = DeserializeInputParameter(xmlReader, parameterType, parameterInfo.Name, operation.Contract.Namespace, parameterInfo);
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

				if (messageContractAttribute.IsWrapped)
				{
					// It's wrapped so we treat it like normal!
					arguments[parameterInfo.Index] = DeserializeInputParameter(xmlReader, parameterInfo.Parameter.ParameterType, parameterInfo.Name, @namespace, parameterInfo);
				}
				else
				{
					// This object isn't a wrapper element, so we will hunt for the nested message body
					// member inside of it
					var messageBodyMembers =
						parameterType
							.GetPropertyOrFieldMembers()
							.Select(mi => new
							{
								Member = mi,
								MessageBodyMemberAttribute = mi.GetCustomAttribute<MessageBodyMemberAttribute>()
							})
							.OrderBy(x => x.MessageBodyMemberAttribute.Order);

					var wrapperObject = Activator.CreateInstance(parameterInfo.Parameter.ParameterType);

					foreach (var messageBodyMember in messageBodyMembers)
					{
						var messageBodyMemberAttribute = messageBodyMember.MessageBodyMemberAttribute;
						var messageBodyMemberInfo = messageBodyMember.Member;

						var innerParameterName = messageBodyMemberAttribute.Name ?? messageBodyMemberInfo.Name;
						var innerParameterNs = messageBodyMemberAttribute.Namespace;
						var innerParameterType = messageBodyMemberInfo.GetPropertyOrFieldType();

						var innerParameter = DeserializeInputParameter(xmlReader, innerParameterType, innerParameterName, innerParameterNs, parameterInfo);

						if (messageBodyMemberInfo is FieldInfo fi)
						{
							fi.SetValue(wrapperObject, innerParameter);
						}
						else if (messageBodyMemberInfo is PropertyInfo pi)
						{
							pi.SetValue(wrapperObject, innerParameter);
						}
						else
						{
							throw new NotImplementedException("Cannot set value of parameter type from " + messageBodyMemberInfo.GetType()?.Name);
						}
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

		private object DeserializeInputParameter(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, SoapMethodParameterInfo parameterInfo)
		{
			if (xmlReader.IsStartElement(parameterName, parameterNs))
			{
				xmlReader.MoveToStartElement(parameterName, parameterNs);

				if (xmlReader.IsStartElement(parameterName, parameterNs))
				{
					switch (_serializer)
					{
						case SoapSerializer.XmlWcfSerializer:
						case SoapSerializer.XmlSerializer:
							if (!parameterType.IsArray || (parameterInfo.ArrayName != null && parameterInfo.ArrayItemName == null))
							{
								// case [XmlElement("parameter")] int parameter
								// case int[] parameter
								// case [XmlArray("parameter")] int[] parameter
								return DeserializeObject(xmlReader, parameterType, parameterName, parameterNs);
							}
							else
							{
								// case [XmlElement("parameter")] int[] parameter
								// case [XmlArray("parameter"), XmlArrayItem(ElementName = "item")] int[] parameter
								return DeserializeArray(xmlReader, parameterType, parameterName, parameterNs, parameterInfo);
							}

						case SoapSerializer.DataContractSerializer:
							return DeserializeDataContract(xmlReader, parameterType, parameterName, parameterNs);

						default:
							throw new NotImplementedException();
					}
				}
			}

			return null;
		}

		private object DeserializeDataContract(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs)
		{
			var elementType = parameterType.GetElementType();

			if (elementType == null || parameterType.IsArray)
			{
				elementType = parameterType;
			}

			var serializer = new DataContractSerializer(elementType, parameterName, parameterNs);

			return serializer.ReadObject(xmlReader, verifyObjectName: true);
		}

		private object DeserializeArray(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs, SoapMethodParameterInfo parameterInfo)
		{
			//if (parameterInfo.ArrayItemName != null)
			{
				xmlReader.ReadStartElement(parameterName, parameterNs);
			}

			var elementType = parameterType.GetElementType();

			var localName = parameterInfo.ArrayItemName ?? elementType.Name;
			if (parameterInfo.ArrayItemName == null && elementType.Namespace.StartsWith("System"))
			{
				var compiler = new CSharpCodeProvider();
				var type = new CodeTypeReference(elementType);
				localName = compiler.GetTypeOutput(type);
			}

			//localName = "ComplexModelInput";
			var deserializeMethod = typeof(XmlSerializerExtensions).GetGenericMethod(nameof(XmlSerializerExtensions.DeserializeArray), new[] { elementType });
			var serializer = CachedXmlSerializer.GetXmlSerializer(elementType, localName, parameterNs);

			object result = null;

			lock (serializer)
			{
				result = deserializeMethod.Invoke(null, new object[] { serializer, localName, parameterNs, xmlReader });
			}

			//if (parameterInfo.ArrayItemName != null)
			{
				xmlReader.ReadEndElement();
			}

			return result;
		}

		private object DeserializeObject(System.Xml.XmlDictionaryReader xmlReader, Type parameterType, string parameterName, string parameterNs)
		{
			// see https://referencesource.microsoft.com/System.Xml/System/Xml/Serialization/XmlSerializer.cs.html#c97688a6c07294d5
			var elementType = parameterType.GetElementType();

			if (elementType == null || parameterType.IsArray)
			{
				elementType = parameterType;
			}

			var serializer = CachedXmlSerializer.GetXmlSerializer(elementType, parameterName, parameterNs);

			lock (serializer)
			{
				return serializer.Deserialize(xmlReader);
			}
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
