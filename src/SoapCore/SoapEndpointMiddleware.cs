using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SoapCore.DocumentationWriter;
using SoapCore.Extensibility;
using SoapCore.MessageEncoder;
using SoapCore.Meta;
using SoapCore.Serializer;
using SoapCore.ServiceModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;

namespace SoapCore
{
	public class SoapEndpointMiddleware<T_MESSAGE>
		where T_MESSAGE : CustomMessage, new()
	{
		private readonly ILogger<SoapEndpointMiddleware<T_MESSAGE>> _logger;
		private readonly RequestDelegate _next;
		private readonly SoapOptions _options;
		private readonly IServiceProvider _serviceProvider;
		private readonly ServiceDescription _service;
		private readonly StringComparison _pathComparisonStrategy;
		private readonly SoapMessageEncoder[] _messageEncoders;
		private readonly IXmlSerializationHandler _serializerHandler;

		[Obsolete]
		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware<T_MESSAGE>> logger, RequestDelegate next, IServiceProvider serviceProvider, Type serviceType, string path, SoapEncoderOptions[] encoderOptions, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding, bool httpGetEnabled, bool httpsGetEnabled)
			: this(
				  logger,
				  next,
				  new SoapOptions()
				  {
					  ServiceType = serviceType,
					  Path = path,
					  EncoderOptions = encoderOptions ?? binding?.ToEncoderOptions(),
					  SoapSerializer = serializer,
					  CaseInsensitivePath = caseInsensitivePath,
					  SoapModelBounder = soapModelBounder,
					  UseBasicAuthentication = binding.HasBasicAuth(),
					  HttpGetEnabled = httpGetEnabled,
					  HttpsGetEnabled = httpsGetEnabled
				  },
				  serviceProvider)
		{
		}

		public SoapEndpointMiddleware(
			ILogger<SoapEndpointMiddleware<T_MESSAGE>> logger,
			RequestDelegate next,
			SoapOptions options,
			IServiceProvider serviceProvider)
		{
			_logger = logger;
			_next = next;
			_options = options;
			_serviceProvider = serviceProvider;

			var serializerResolver = _serviceProvider.GetService<IXmlSerializationHandlerResolver>();
			if (serializerResolver != null && _options.SerializerIdentifier != null)
			{
				_serializerHandler = serializerResolver(_options.SerializerIdentifier);
				_ = _serializerHandler ?? throw new InvalidOperationException("custom serializer implementation not found.");
			}

			_serializerHandler ??= new SerializerHelper(options.SoapSerializer);

			_pathComparisonStrategy = options.CaseInsensitivePath ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_service = new ServiceDescription(options.ServiceType, options.GenerateSoapActionWithoutContractName);

			options.EncoderOptions ??= new[] { new SoapEncoderOptions() };

			_messageEncoders = new SoapMessageEncoder[options.EncoderOptions.Length];

			for (var i = 0; i < options.EncoderOptions.Length; i++)
			{
				var encoderOptions = options.EncoderOptions[i];
				_messageEncoders[i] = new SoapMessageEncoder(encoderOptions.MessageVersion, encoderOptions.WriteEncoding, encoderOptions.OverwriteResponseContentType, encoderOptions.ReaderQuotas, options.OmitXmlDeclaration, options.CheckXmlCharacters, encoderOptions.XmlNamespaceOverrides, encoderOptions.BindingName, encoderOptions.PortName, options.NormalizeNewLines, encoderOptions.MaxSoapHeaderSize);
			}
		}

		public async Task Invoke(HttpContext httpContext)
		{
			var trailPathTuner = _serviceProvider.GetService<TrailingServicePathTuner>();

			trailPathTuner?.ConvertPath(httpContext);

			var requestMethod = httpContext.Request.Method;

			if (httpContext.Request.Path.StartsWithSegments(_options.Path, _pathComparisonStrategy, out var remainingPath))
			{
				requestMethod = requestMethod?.ToLower();
				if (requestMethod == "head")
				{
					// Since there's no information about what you should do with HEAD requests for SOAP APIs, we just silently return "200 OK"
					httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
					return;
				}

				if (requestMethod == "get")
				{
					// If GET is not enabled, either for HTTP or HTTPS, return a 403 instead of the WSDL
					if ((httpContext.Request.IsHttps && !_options.HttpsGetEnabled) || (!httpContext.Request.IsHttps && !_options.HttpGetEnabled))
					{
						httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
						return;
					}
				}

				try
				{
					_logger.LogDebug("Received SOAP Request for {0} ({1} bytes)", httpContext.Request.Path, httpContext.Request.ContentLength ?? 0);

					if (requestMethod == "get")
					{
						if (!string.IsNullOrWhiteSpace(remainingPath))
						{
							await ProcessHttpOperation(httpContext, _serviceProvider, remainingPath.Value.Trim('/'));
						}
						else if (httpContext.Request.Query.ContainsKey("xsd") && _options.WsdlFileOptions != null)
						{
							await ProcessXSD(httpContext);
						}
						else if (httpContext.Request.Query.ContainsKey("import") && _options.WsdlFileOptions != null)
						{
							await ProcessWsdlImport(httpContext);
						}
						else if (string.IsNullOrEmpty(httpContext.Request.ContentType) || httpContext.Request.Query.ContainsKey("wsdl"))
						{
							// Shows automatically generated documentation based on the generated WSDL (WIP)
							var showDocumentation = httpContext.Request.Query.ContainsKey("documentation");

							if (_options.WsdlFileOptions != null)
							{
								await ProcessMetaFromFile(httpContext, showDocumentation);
							}
							else
							{
								await ProcessMeta(httpContext, showDocumentation);
							}
						}
					}
					else
					{
						if (!string.IsNullOrWhiteSpace(remainingPath))
						{
							if ((httpContext.Request.IsHttps && !_options.HttpsPostEnabled) || (!httpContext.Request.IsHttps && !_options.HttpPostEnabled))
							{
								httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
								return;
							}

							await ProcessHttpOperation(httpContext, _serviceProvider, remainingPath.Value.Trim('/'));
						}
						else
						{
							await ProcessOperation(httpContext, _serviceProvider);
						}
					}
				}
				catch (Exception ex)
				{
					_logger.LogCritical(ex, "An error occurred when trying to service a request on SOAP endpoint: {0}", httpContext.Request.Path);

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

#if !NETCOREAPP3_0_OR_GREATER
		private static Task WriteMessageAsync(SoapMessageEncoder messageEncoder, Message responseMessage, HttpContext httpContext, bool indentXml)
		{
			return messageEncoder.WriteMessageAsync(responseMessage, httpContext, httpContext.Response.Body, indentXml);
		}
#else
		private static Task WriteMessageAsync(SoapMessageEncoder messageEncoder, Message responseMessage, HttpContext httpContext, bool indentXml)
		{
			return messageEncoder.WriteMessageAsync(responseMessage, httpContext, httpContext.Response.BodyWriter, indentXml);
		}
#endif

		private static string TryGetMultipartBoundary(HttpRequest request)
		{
			var parsedContentType = MediaTypeHeaderValue.Parse(request.ContentType);
			if (parsedContentType.MediaType != "multipart/related")
			{
				return null;
			}

			var boundaryValue = parsedContentType.Parameters
				.FirstOrDefault(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase))
				?.Value;

			if (string.IsNullOrWhiteSpace(boundaryValue))
			{
				return null;
			}

			return boundaryValue.Trim('"');
		}

		private async Task<Message> ReadMessageAsync(HttpContext httpContext, SoapMessageEncoder messageEncoder)
		{
			var boundary = TryGetMultipartBoundary(httpContext.Request);

			if (!string.IsNullOrWhiteSpace(boundary))
			{
				var multipartReader = new MultipartReader(boundary, httpContext.Request.Body);

				while (true)
				{
					var multipartSection = await multipartReader.ReadNextSectionAsync();

					if (multipartSection == null)
					{
						break;
					}

					if (messageEncoder.IsContentTypeSupported(multipartSection.ContentType, true)
						|| messageEncoder.IsContentTypeSupported(multipartSection.ContentType, false))
					{
						return await messageEncoder.ReadMessageAsync(multipartSection.Body, messageEncoder.MaxSoapHeaderSize, multipartSection.ContentType);
					}
				}
			}
#if !NETCOREAPP3_0_OR_GREATER
			return await messageEncoder.ReadMessageAsync(httpContext.Request.Body, messageEncoder.MaxSoapHeaderSize, httpContext.Request.ContentType);
#else
			return await messageEncoder.ReadMessageAsync(httpContext.Request.BodyReader, messageEncoder.MaxSoapHeaderSize, httpContext.Request.ContentType);
#endif
		}

		private async Task ProcessMeta(HttpContext httpContext, bool showDocumentation)
		{
			var baseUrl = httpContext.Request.Scheme + "://" + httpContext.Request.Host + httpContext.Request.PathBase + httpContext.Request.Path;
			var xmlNamespaceManager = GetXmlNamespaceManager(null);
			var bindingName = !string.IsNullOrWhiteSpace(_options.EncoderOptions[0].BindingName) ? _options.EncoderOptions[0].BindingName : "BasicHttpBinding_" + _service.GeneralContract.Name;
			var bodyWriter = _options.SoapSerializer == SoapSerializer.XmlSerializer
				? new MetaBodyWriter(_service, baseUrl, xmlNamespaceManager, bindingName, _messageEncoders.Select(me => new SoapBindingInfo(me.MessageVersion, me.BindingName, me.PortName)).ToArray(), _options.UseMicrosoftGuid, _options.WsdlOperationNameGenerator)
				: (BodyWriter)new MetaWCFBodyWriter(_service, baseUrl, bindingName, _options.UseBasicAuthentication, _messageEncoders.Select(me => new SoapBindingInfo(me.MessageVersion, me.BindingName, me.PortName)).ToArray(), _options.WsdlOperationNameGenerator);

			//assumption that you want soap12 if your service supports that
			var messageEncoder = _messageEncoders.FirstOrDefault(me => me.MessageVersion == MessageVersion.Soap12WSAddressing10 || me.MessageVersion == MessageVersion.Soap12WSAddressingAugust2004) ?? _messageEncoders[0];
			var soapVersions = _messageEncoders.Select(me => me.MessageVersion).Distinct().ToArray();

			using var responseMessage = new MetaMessage(
				Message.CreateMessage(messageEncoder.MessageVersion, null, bodyWriter),
				_service,
				GetXmlNamespaceManager(messageEncoder),
				bindingName,
				_options.UseBasicAuthentication,
				soapVersions);

			if (showDocumentation)
			{
				httpContext.Response.ContentType = "text/html;charset=UTF-8";

				var ms = new MemoryStream();
				await messageEncoder.WriteMessageAsync(responseMessage, httpContext, ms, _options.IndentWsdl);
				ms.Position = 0;
				var documentation = SoapDefinition.DeserializeFromStream(ms).GenerateDocumentation();
				if (httpContext?.Response.ContentLength <= documentation.Length)
				{
					httpContext.Response.ContentLength = documentation.Length;
				}
				await httpContext.Response.WriteAsync(documentation);

				return;
			}

			//we should use text/xml in wsdl page for browser compability.
			httpContext.Response.ContentType = "text/xml;charset=UTF-8"; // _messageEncoders[0].ContentType;

			await WriteMessageAsync(messageEncoder, responseMessage, httpContext, _options.IndentWsdl);
		}

		private async Task ProcessOperation(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			// Get the encoder based on Content Type
			var messageEncoder = _messageEncoders.FirstOrDefault(me => me.IsContentTypeSupported(httpContext.Request.ContentType, true))
				?? _messageEncoders.FirstOrDefault(me => me.IsContentTypeSupported(httpContext.Request.ContentType, false))
				?? _messageEncoders[0];

			Message requestMessage = null;
			Message responseMessage = null;

			try
			{
				//Get the message
				requestMessage = await ReadMessageAsync(httpContext, messageEncoder);

				var asyncMessageFilters = serviceProvider.GetServices<IAsyncMessageFilter>().ToArray();

				foreach (var messageFilter in asyncMessageFilters)
				{
					await messageFilter.OnRequestExecuting(requestMessage);
				}

				var soapMessageProcessors = serviceProvider.GetServices<ISoapMessageProcessor>().ToArray();

				var processorPipe = MakeProcessorPipe(soapMessageProcessors, httpContext, (requestMessage) => ProcessMessage(requestMessage, messageEncoder, asyncMessageFilters, httpContext, serviceProvider));

				responseMessage = await processorPipe(requestMessage);
			}
			catch (Exception ex)
			{
				var status = StatusCodes.Status500InternalServerError;
				if (ex is TargetInvocationException targetInvocationException)
				{
					ex = targetInvocationException.InnerException;
				}
				else if (ex is AuthenticationException)
				{
					status = StatusCodes.Status401Unauthorized;
				}
				else if (ex is UnauthorizedAccessException)
				{
					status = StatusCodes.Status403Forbidden;
				}
				else if (ex is XmlException)
				{
					status = StatusCodes.Status400BadRequest;
				}
				else if (ex is ConnectionResetException)
				{
					status = StatusCodes.Status400BadRequest;
				}

				responseMessage = CreateErrorResponseMessage(ex, status, serviceProvider, requestMessage, messageEncoder, httpContext);
			}

			if (responseMessage != null)
			{
				await WriteMessageAsync(messageEncoder, responseMessage, httpContext, _options.IndentXml);
			}
		}

		private async Task ProcessHttpOperation(HttpContext context, IServiceProvider serviceProvider, string methodName)
		{
			if (!TryGetOperation(methodName, out var operation))
			{
				context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
				await context.Response.WriteAsync($"Service does not support \"/{methodName}\"");
				return;
			}

			var arguments = new object[operation.AllParameters.Length];

			var missingParameters = new List<string>();

			bool TryGetRequestValue(string key, out StringValues value)
			{
				if (context.Request.Method?.ToLower() == "get")
				{
					return context.Request.Query.TryGetValue(key, out value);
				}

				return context.Request.Form.TryGetValue(key, out value);
			}

			foreach (var parameter in operation.InParameters)
			{
				var baseType = parameter.Parameter.ParameterType;
				var nullableType = Nullable.GetUnderlyingType(baseType);
				if (TryGetRequestValue(parameter.Name, out var requestValue))
				{
					arguments[parameter.Index] = Convert.ChangeType(requestValue.ToString(), nullableType ?? baseType);
				}
				else
				{
					if (nullableType == null)
					{
						missingParameters.Add(parameter.Name);
					}
				}
			}

			if (missingParameters.Count > 0)
			{
				context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
				await context.Response.WriteAsync($"Missing value for parameter(s): \"{methodName}\", ({string.Join(", ", missingParameters)})");
				return;
			}

			var httpContextParameter = operation.InParameters.FirstOrDefault(x => x.Parameter.ParameterType == typeof(HttpContext));
			if (httpContextParameter != default)
			{
				arguments[httpContextParameter.Index] = context;
			}

			var serviceInstance = serviceProvider.GetRequiredService(_service.ServiceType);
			var invoker = serviceProvider.GetService<IOperationInvoker>() ?? new DefaultOperationInvoker();
			var responseObject = await invoker.InvokeAsync(operation.DispatchMethod, serviceInstance, arguments);

			// If response has an HTTP status code attached
			if (responseObject is IConvertToActionResult convertToActionResult)
			{
				responseObject = convertToActionResult.Convert();
			}

			if (responseObject is IActionResult actionResult)
			{
				var type = actionResult.GetType();
				context.Response.StatusCode = (int)(actionResult
					.GetType()
					.GetProperty("StatusCode")?
					.GetValue(actionResult, null) ?? HttpStatusCode.OK);
				responseObject = actionResult
					.GetType()
					.GetProperty("Value")?
					.GetValue(actionResult, null);
			}
			else if (operation.IsOneWay)
			{
				context.Response.StatusCode = (int)HttpStatusCode.Accepted;
				return;
			}
			else
			{
				context.Response.StatusCode = (int)HttpStatusCode.OK;
			}

			var resultOutDictionary = new Dictionary<string, object>();
			foreach (var parameterInfo in operation.OutParameters)
			{
				resultOutDictionary[parameterInfo.Name] = arguments[parameterInfo.Index];
			}

			var bodyWriter = new ServiceBodyWriter(_options.SoapSerializer, operation, responseObject, resultOutDictionary, true);

			context.Response.ContentType = "text/xml";

			var ms = new MemoryStream();
			XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings
			{
				Encoding = DefaultEncodings.UTF8,

			});
			XmlDictionaryWriter dictionaryWriter = XmlDictionaryWriter.CreateDictionaryWriter(writer);

			bodyWriter.WriteBodyContents(dictionaryWriter);
			dictionaryWriter.Flush();
			ms.Seek(0, SeekOrigin.Begin);
			await ms.CopyToAsync(context.Response.Body);
		}

		private Func<Message, Task<Message>> MakeProcessorPipe(ISoapMessageProcessor[] soapMessageProcessors, HttpContext httpContext, Func<Message, Task<Message>> processMessageFunc)
		{
			Func<Message, Task<Message>> MakeProcessorPipe(int i = 0)
			{
				if (i < soapMessageProcessors.Length)
				{
					return (requestMessage) => soapMessageProcessors[i].ProcessMessage(requestMessage, httpContext, MakeProcessorPipe(i + 1));
				}
				else
				{
					return processMessageFunc;
				}
			}

			return MakeProcessorPipe();
		}

		private async Task<Message> ProcessMessage(Message requestMessage, SoapMessageEncoder messageEncoder, IAsyncMessageFilter[] asyncMessageFilters, HttpContext httpContext, IServiceProvider serviceProvider)
		{
			Message responseMessage;
			var soapAction = HeadersHelper.GetSoapAction(httpContext, ref requestMessage);
			requestMessage.Headers.Action = soapAction;

			if (string.IsNullOrEmpty(soapAction))
			{
				throw new ArgumentException($"Unable to handle request without a valid action parameter. Please supply a valid soap action.");
			}

			var correlationObjects2 = default(List<(IMessageInspector2 inspector, object correlationObject)>);
			using (IServiceScope scope = serviceProvider.CreateScope())
			{
				var messageInspector2s = scope.ServiceProvider.GetServices<IMessageInspector2>();
				correlationObjects2 = messageInspector2s.Select(mi =>
					(inspector: mi, correlationObject: mi.AfterReceiveRequest(ref requestMessage, _service)))
					.ToList();
			}

			// for getting soapaction and parameters in (optional) body
			// GetReaderAtBodyContents must not be called twice in one request
			XmlDictionaryReader reader = null;
			if (!requestMessage.IsEmpty)
			{
				reader = requestMessage.GetReaderAtBodyContents();
			}

			try
			{
				if (!TryGetOperation(soapAction, out var operation))
				{
					throw new InvalidOperationException($"No operation found for specified action: {soapAction}");
				}

				_logger.LogInformation("Request for operation {0}.{1} received", operation.Contract.Name, operation.Name);

				//Create an instance of the service class
				var serviceInstance = serviceProvider.GetRequiredService(_service.ServiceType);

				SetMessageHeadersToProperty(requestMessage, serviceInstance);

				// Get operation arguments from message
				var arguments = GetRequestArguments(requestMessage, reader, operation, httpContext);

				ExecuteFiltersAndTune(httpContext, serviceProvider, operation, arguments, serviceInstance);

				var invoker = serviceProvider.GetService<IOperationInvoker>() ?? new DefaultOperationInvoker();
				var responseObject = await invoker.InvokeAsync(operation.DispatchMethod, serviceInstance, arguments);

				// If response has an HTTP status code attached
				if (responseObject is IConvertToActionResult convertToActionResult)
				{
					responseObject = convertToActionResult.Convert();
				}

				if (responseObject is IActionResult actionResult)
				{
					var type = actionResult.GetType();
					httpContext.Response.StatusCode = (int)(actionResult
						.GetType()
						.GetProperty("StatusCode")?
						.GetValue(actionResult, null) ?? HttpStatusCode.OK);
					responseObject = null;
					responseObject = actionResult
						.GetType()
						.GetProperty("Value")?
						.GetValue(actionResult, null);
				}
				else if (operation.IsOneWay)
				{
					httpContext.Response.StatusCode = (int)HttpStatusCode.Accepted;
					return null;
				}

				var resultOutDictionary = new Dictionary<string, object>();
				foreach (var parameterInfo in operation.OutParameters)
				{
					resultOutDictionary[parameterInfo.Name] = arguments[parameterInfo.Index];
				}

				responseMessage = CreateResponseMessage(operation, responseObject, resultOutDictionary, soapAction, requestMessage, messageEncoder);

				httpContext.Response.ContentType = httpContext.Request.ContentType;
				httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;

				correlationObjects2.ForEach(mi => mi.inspector.BeforeSendReply(ref responseMessage, _service, mi.correlationObject));
			}
			finally
			{
				reader?.Dispose();
			}

			// Execute response message filters
			foreach (var messageFilter in asyncMessageFilters.Reverse())
			{
				await messageFilter.OnResponseExecuting(responseMessage);
			}

			SetHttpResponse(httpContext, responseMessage);
			return responseMessage;
		}

		private bool TryGetOperation(string methodName, out OperationDescription operation)
		{
			operation = _service.Operations.FirstOrDefault(o => o.SoapAction.Equals(methodName, StringComparison.OrdinalIgnoreCase)
							|| o.Name.Equals(HeadersHelper.GetTrimmedSoapAction(methodName), StringComparison.OrdinalIgnoreCase)
							|| methodName.Equals(HeadersHelper.GetTrimmedSoapAction(o.Name), StringComparison.OrdinalIgnoreCase));

			if (operation == null)
			{
				operation = _service.Operations.FirstOrDefault(o =>
							methodName.Equals(HeadersHelper.GetTrimmedClearedSoapAction(o.SoapAction), StringComparison.OrdinalIgnoreCase)
							|| methodName.IndexOf(HeadersHelper.GetTrimmedSoapAction(o.Name), StringComparison.OrdinalIgnoreCase) >= 0);
			}

			return operation != null;
		}

		private Message CreateResponseMessage(
			OperationDescription operation,
			object responseObject,
			Dictionary<string, object> resultOutDictionary,
			string soapAction,
			Message requestMessage,
			SoapMessageEncoder soapMessageEncoder)
		{
			T_MESSAGE responseMessage;

			// Create response message
			var bodyWriter = new ServiceBodyWriter(_options.SoapSerializer, operation, responseObject, resultOutDictionary);
			var xmlNamespaceManager = GetXmlNamespaceManager(soapMessageEncoder);

			if (soapMessageEncoder.MessageVersion.Addressing == AddressingVersion.WSAddressing10)
			{
				responseMessage = new T_MESSAGE
				{
					StandAloneAttribute = _options.StandAloneAttribute,
					Message = Message.CreateMessage(soapMessageEncoder.MessageVersion, soapAction, bodyWriter),
					AdditionalEnvelopeXmlnsAttributes = _options.AdditionalEnvelopeXmlnsAttributes,
					NamespaceManager = xmlNamespaceManager
				};

				responseMessage.Headers.Action = operation.ReplyAction;
				responseMessage.Headers.RelatesTo = requestMessage.Headers.MessageId;
				responseMessage.Headers.To = requestMessage.Headers.ReplyTo?.Uri;
			}
			else
			{
				responseMessage = new T_MESSAGE
				{
					StandAloneAttribute = _options.StandAloneAttribute,
					Message = Message.CreateMessage(soapMessageEncoder.MessageVersion, null, bodyWriter),
					AdditionalEnvelopeXmlnsAttributes = _options.AdditionalEnvelopeXmlnsAttributes,
					NamespaceManager = xmlNamespaceManager
				};
			}

			if (responseObject != null)
			{
				var messageHeaderMembers = responseObject.GetType().GetMembersWithAttribute<MessageHeaderAttribute>();
				foreach (var messageHeaderMember in messageHeaderMembers)
				{
					var messageHeaderAttribute = messageHeaderMember.GetCustomAttribute<MessageHeaderAttribute>();
					responseMessage.Headers.Add(MessageHeader.CreateHeader(messageHeaderAttribute.Name ?? messageHeaderMember.Name, messageHeaderAttribute.Namespace ?? operation.Contract.Namespace, messageHeaderMember.GetPropertyOrFieldValue(responseObject), messageHeaderAttribute.MustUnderstand));
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
			_options.SoapModelBounder?.OnModelBound(operation.DispatchMethod, arguments);

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
		private object[] GetRequestArguments(Message requestMessage, XmlDictionaryReader xmlReader, OperationDescription operation, HttpContext httpContext)
		{
			var arguments = new object[operation.AllParameters.Length];
			var alreadyProcessedParameters = new bool[operation.AllParameters.Length];

			IEnumerable<Type> serviceKnownTypes = operation
				.GetServiceKnownTypesHierarchy()
				.Select(x => x.Type);

			if (!operation.IsMessageContractRequest)
			{
				if (xmlReader != null)
				{
					xmlReader.ReadStartElement(operation.Name, operation.Contract.Namespace);

					var lastParameterIndex = -1;
					while (!xmlReader.EOF)
					{
						var parameterInfo = operation.InParameters.FirstOrDefault(p => p.Name == xmlReader.LocalName);
						if (parameterInfo == null || alreadyProcessedParameters[parameterInfo.Index])
						{
							xmlReader.Skip();
							continue;
						}

						// prevent infinite loop (see https://github.com/DigDes/SoapCore/issues/610)
						if (parameterInfo.Index == lastParameterIndex)
						{
							break;
						}

						lastParameterIndex = parameterInfo.Index;
						alreadyProcessedParameters[lastParameterIndex] = true;

						var argumentValue = _serializerHandler.DeserializeInputParameter(
							xmlReader,
							parameterInfo.Parameter.ParameterType,
							parameterInfo.Name,
							operation.Contract.Namespace,
							parameterInfo.Parameter,
							serviceKnownTypes);

						//fix https://github.com/DigDes/SoapCore/issues/379 (hack, need research)
						if (argumentValue == null)
						{
							argumentValue = _serializerHandler.DeserializeInputParameter(
								xmlReader,
								parameterInfo.Parameter.ParameterType,
								parameterInfo.Name,
								parameterInfo.Namespace,
								parameterInfo.Parameter,
								serviceKnownTypes);
						}

						// sometimes there's no namespace for the parameter (ex. MS SOAP SDK)
						if (argumentValue == null)
						{
							argumentValue = _serializerHandler.DeserializeInputParameter(
								xmlReader,
								parameterInfo.Parameter.ParameterType,
								parameterInfo.Name,
								string.Empty,
								parameterInfo.Parameter,
								serviceKnownTypes);
						}

						arguments[parameterInfo.Index] = argumentValue;
					}

					var httpContextParameter = operation.InParameters.FirstOrDefault(x => x.Parameter.ParameterType == typeof(HttpContext));
					if (httpContextParameter != default)
					{
						arguments[httpContextParameter.Index] = httpContext;
					}
				}
				else
				{
					arguments = Array.Empty<object>();
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
					//https://github.com/DigDes/SoapCore/issues/385
					if (operation.DispatchMethod.GetCustomAttribute<XmlSerializerFormatAttribute>()?.Style == OperationFormatStyle.Rpc)
					{
						DeserializeParameters(requestMessage, xmlReader, parameterType, parameterInfo, @namespace, serviceKnownTypes, messageContractAttribute, arguments);
					}
					else
					{
						// It's wrapped so either the wrapper name or the name of the wrapper type
						arguments[parameterInfo.Index] = _serializerHandler.DeserializeInputParameter(
							xmlReader,
							parameterInfo.Parameter.ParameterType,
							messageContractAttribute.WrapperName ?? parameterInfo.Parameter.ParameterType.Name,
							messageContractAttribute.WrapperNamespace ?? @namespace,
							parameterInfo.Parameter,
							serviceKnownTypes);
					}
				}
				else
				{
					DeserializeParameters(requestMessage, xmlReader, parameterType, parameterInfo, @namespace, serviceKnownTypes, messageContractAttribute, arguments);
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

		// https://github.com/DigDes/SoapCore/issues/575
		private void DeserializeParameters(
			Message requestMessage,
			XmlDictionaryReader xmlReader,
			Type parameterType,
			SoapMethodParameterInfo parameterInfo,
			string @namespace,
			IEnumerable<Type> serviceKnownTypes,
			MessageContractAttribute messageContractAttribute,
			object[] arguments)
		{
			var messageHeadersMembers = parameterType.GetPropertyOrFieldMembers()
				.Where(x => x.GetCustomAttribute<MessageHeaderAttribute>() != null)
				.Select(mi => new
				{
					MemberInfo = mi,
					MessageHeaderMemberAttribute = mi.GetCustomAttribute<MessageHeaderAttribute>()
				}).ToArray();

			var wrapperObject = Activator.CreateInstance(parameterInfo.Parameter.ParameterType);

			for (var i = 0; i < requestMessage.Headers.Count; i++)
			{
				var header = requestMessage.Headers[i];
				var member = messageHeadersMembers.FirstOrDefault(x =>
					x.MessageHeaderMemberAttribute.Name == header.Name || x.MemberInfo.Name == header.Name);

				if (member != null)
				{
					var reader = requestMessage.Headers.GetReaderAtHeader(i);

					var value = _serializerHandler.DeserializeInputParameter(
						reader,
						member.MemberInfo.GetPropertyOrFieldType(),
						member.MessageHeaderMemberAttribute.Name ?? member.MemberInfo.Name,
						member.MessageHeaderMemberAttribute.Namespace ?? @namespace,
						member.MemberInfo,
						serviceKnownTypes);

					member.MemberInfo.SetValueToPropertyOrField(wrapperObject, value);
				}
			}

			var messageBodyMembers = parameterType.GetPropertyOrFieldMembers()
				.Where(x => x.GetCustomAttribute<MessageBodyMemberAttribute>() != null).Select(mi => new
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

				var innerParameter = _serializerHandler.DeserializeInputParameter(
					xmlReader,
					innerParameterType,
					innerParameterName,
					innerParameterNs,
					messageBodyMemberInfo,
					serviceKnownTypes);

				messageBodyMemberInfo.SetValueToPropertyOrField(wrapperObject, innerParameter);
			}

			arguments[parameterInfo.Index] = wrapperObject;
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
		/// <param name="messageEncoder">
		/// Message encoder of incoming request
		/// </param>
		/// <param name="httpContext">
		/// The HTTP context that received the response message.
		/// </param>
		/// <returns>
		/// Returns the constructed message (which is implicitly written to the response
		/// and therefore must not be handled by the caller).
		/// </returns>
		private Message CreateErrorResponseMessage(
			Exception exception,
			int statusCode,
			IServiceProvider serviceProvider,
			Message requestMessage,
			SoapMessageEncoder messageEncoder,
			HttpContext httpContext)
		{
			_logger.LogError(exception, "An error occurred processing the message");

			var xmlNamespaceManager = GetXmlNamespaceManager(messageEncoder);
			var faultExceptionTransformer = serviceProvider.GetRequiredService<IFaultExceptionTransformer>();
			var faultMessage = faultExceptionTransformer.ProvideFault(exception, messageEncoder.MessageVersion, requestMessage, xmlNamespaceManager);

			if (!httpContext.Response.HasStarted)
			{
				httpContext.Response.ContentType = httpContext.Request.ContentType;
				httpContext.Response.Headers["SOAPAction"] = faultMessage.Headers.Action;
				httpContext.Response.StatusCode = statusCode;
			}

			SetHttpResponse(httpContext, faultMessage);
			if (messageEncoder.MessageVersion.Addressing == AddressingVersion.WSAddressing10)
			{
				// TODO: Some additional work needs to be done in order to support setting the action. Simply setting it to
				// "http://www.w3.org/2005/08/addressing/fault" will cause the WCF Client to not be able to figure out the type
				faultMessage.Headers.RelatesTo = requestMessage?.Headers.MessageId;
				faultMessage.Headers.To = requestMessage?.Headers.ReplyTo?.Uri;
			}

			return faultMessage;
		}

		private void SetHttpResponse(HttpContext httpContext, Message message)
		{
			if (!message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out var value)
				|| value is not HttpResponseMessageProperty httpProperty)
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

		private string GetServerUrl(WsdlFileOptions options, HttpContext httpContext)
		{
			if (!string.IsNullOrEmpty(options.UrlOverride))
			{
				return options.UrlOverride;
			}

			if (options.UrlOverrideFunc != null)
			{
				return options.UrlOverrideFunc(options, httpContext);
			}

			string scheme = string.IsNullOrEmpty(options.SchemeOverride) ? httpContext.Request.Scheme : options.SchemeOverride;
			string host = httpContext.Request.Host.ToString();
			var forwardedHost = httpContext.Request.Headers["X-Forwarded-Host"];
			if (forwardedHost.Count != 0)
			{
				host = forwardedHost[0];
			}

			return scheme + "://" + host + "/";
		}

		private MetaFromFile GetMeta(HttpContext httpContext)
		{
			var options = _options.WsdlFileOptions;
			var meta = new MetaFromFile();
			if (!string.IsNullOrEmpty(options.VirtualPath))
			{
				meta.CurrentWebServer = options.VirtualPath + "/";
			}

			meta.CurrentWebService = httpContext.Request.Path.Value.Replace("/", string.Empty);
			var mapping = options.WebServiceWSDLMapping[meta.CurrentWebService];

			meta.WSDLFolder = mapping.WSDLFolder;
			meta.XsdFolder = mapping.SchemaFolder;
			meta.ServerUrl = GetServerUrl(options, httpContext);
			return meta;
		}

		private async Task ProcessXSD(HttpContext httpContext)
		{
			var meta = GetMeta(httpContext);
			string xsdFile = httpContext.Request.Query["name"];

			//Check to prevent path traversal
			if (string.IsNullOrEmpty(xsdFile) || Path.GetFileName(xsdFile) != xsdFile)
			{
				throw new ArgumentNullException("xsd parameter contains illegal values");
			}

			if (!xsdFile.Contains(".xsd") && !xsdFile.Contains(".xml"))
			{
				throw new Exception("xsd request must contain .xsd or .xml");
			}

			string path = _options.WsdlFileOptions.AppPath;
			string safePath = path + Path.AltDirectorySeparatorChar + meta.XsdFolder + Path.AltDirectorySeparatorChar + xsdFile;
			string xsd = await meta.ReadLocalFileAsync(safePath);
			string modifiedXsd = meta.ModifyXSDAddRightSchemaPath(xsd);

			//we should use text/xml in wsdl page for browser compability.
			httpContext.Response.ContentType = "text/xml;charset=UTF-8";
			await httpContext.Response.WriteAsync(modifiedXsd);
		}

		private async Task ProcessWsdlImport(HttpContext httpContext)
		{
			var meta = GetMeta(httpContext);
			string wsdlFile = httpContext.Request.Query["name"];

			//Check to prevent path traversal
			if (string.IsNullOrEmpty(wsdlFile) || Path.GetFileName(wsdlFile) != wsdlFile)
			{
				throw new ArgumentNullException("xsd parameter contains illegal values");
			}

			if (!wsdlFile.Contains(".wsdl") && !wsdlFile.Contains(".xml"))
			{
				throw new Exception("import request must contain .wsdl or .xml");
			}

			string path = _options.WsdlFileOptions.AppPath;
			string safePath = path + Path.AltDirectorySeparatorChar + meta.WSDLFolder + Path.AltDirectorySeparatorChar + wsdlFile;
			string wsdl = await meta.ReadLocalFileAsync(safePath);
			string modifiedWsdl = meta.ModifyWSDLAddRightSchemaPath(wsdl);

			//we should use text/xml in wsdl page for browser compability.
			httpContext.Response.ContentType = "text/xml;charset=UTF-8";
			await httpContext.Response.WriteAsync(modifiedWsdl);
		}

		private async Task ProcessMetaFromFile(HttpContext httpContext, bool showDocumentation)
		{
			var meta = new MetaFromFile();

			var url = httpContext.Request.Path.Value.Replace("/", string.Empty);

			var options = _options.WsdlFileOptions;
			var mapping = options.WebServiceWSDLMapping[url];

			if (!string.IsNullOrEmpty(options.VirtualPath))
			{
				meta.CurrentWebServer = options.VirtualPath + "/";
			}

			if (string.IsNullOrEmpty(mapping.UrlOverride))
			{
				meta.CurrentWebService = url;
			}
			else
			{
				meta.CurrentWebService = mapping.UrlOverride;
			}

			meta.WSDLFolder = mapping.WSDLFolder;
			meta.XsdFolder = mapping.SchemaFolder;
			meta.ServerUrl = GetServerUrl(options, httpContext);

			string wsdlfile = mapping.WsdlFile;

			string path = options.AppPath;
			string wsdl = await meta.ReadLocalFileAsync(path + Path.AltDirectorySeparatorChar + meta.WSDLFolder + Path.AltDirectorySeparatorChar + wsdlfile);
			string modifiedWsdl = meta.ModifyWSDLAddRightSchemaPath(wsdl);

			if (showDocumentation)
			{
				httpContext.Response.ContentType = "text/html;charset=UTF-8";

				var documentation = SoapDefinition.DeserializeFromString(modifiedWsdl).GenerateDocumentation();

				await httpContext.Response.WriteAsync(documentation);

				return;
			}

			//we should use text/xml in wsdl page for browser compability.
			httpContext.Response.ContentType = "text/xml;charset=UTF-8";
			await httpContext.Response.WriteAsync(modifiedWsdl);
		}

		private XmlNamespaceManager GetXmlNamespaceManager(SoapMessageEncoder messageEncoder)
		{
			var xmlNamespaceManager = Namespaces.CreateDefaultXmlNamespaceManager(_options.UseMicrosoftGuid);

			xmlNamespaceManager.AddNamespace("tns", _service.GeneralContract.Namespace);

			if (_options.XmlNamespacePrefixOverrides != null)
			{
				foreach (var ns in _options.XmlNamespacePrefixOverrides.GetNamespacesInScope(XmlNamespaceScope.Local))
				{
					xmlNamespaceManager.AddNamespace(ns.Key, ns.Value);
				}
			}

			if (messageEncoder?.XmlNamespaceOverrides != null)
			{
				foreach (var ns in messageEncoder.XmlNamespaceOverrides.GetNamespacesInScope(XmlNamespaceScope.Local))
				{
					xmlNamespaceManager.AddNamespace(ns.Key, ns.Value);
				}
			}

			return xmlNamespaceManager;
		}
	}
}
