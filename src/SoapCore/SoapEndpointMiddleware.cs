using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SoapCore
{
	public class SoapEndpointMiddleware
	{
		private readonly ILogger<SoapEndpointMiddleware> _logger;
		private readonly RequestDelegate _next;
		private readonly ServiceDescription _service;
		private readonly string _endpointPath;
		private readonly MessageEncoder[] _messageEncoders;
		private readonly SoapSerializer _serializer;
		private readonly Binding _binding;
		private readonly StringComparison _pathComparisonStrategy;
		private readonly ISoapModelBounder _soapModelBounder;

		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware> logger, RequestDelegate next, Type serviceType, string path, MessageEncoder[] encoders, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding)
		{
			_logger = logger;
			_next = next;
			_endpointPath = path;
			_messageEncoders = encoders;
			_serializer = serializer;
			_pathComparisonStrategy = caseInsensitivePath ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_service = new ServiceDescription(serviceType);
			_soapModelBounder = soapModelBounder;
			_binding = binding;
		}

		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware> logger, RequestDelegate next, Type serviceType, string path, MessageEncoder encoder, SoapSerializer serializer, bool caseInsensitivePath, ISoapModelBounder soapModelBounder, Binding binding)
			: this(logger, next, serviceType, path, new MessageEncoder[] { encoder }, serializer, caseInsensitivePath, soapModelBounder, binding)
		{
		}

		public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware> logger, RequestDelegate next, SoapOptions options)
			: this(logger, next, options.ServiceType, options.Path, options.MessageEncoders, options.SoapSerializer, options.CaseInsensitivePath, options.SoapModelBounder, options.Binding)
		{
		}

		public async Task Invoke(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			httpContext.Request.EnableRewind();
			var trailPathTuner = serviceProvider.GetServices<TrailingServicePathTuner>().FirstOrDefault();
			if (trailPathTuner != null)
			{
				trailPathTuner.ConvertPath(httpContext);
			}

			if (httpContext.Request.Path.Equals(_endpointPath, _pathComparisonStrategy))
			{
				_logger.LogDebug($"Received SOAP Request for {httpContext.Request.Path} ({httpContext.Request.ContentLength ?? 0} bytes)");

				if (httpContext.Request.Query.ContainsKey("wsdl") && httpContext.Request.Method?.ToLower() == "get")
				{
					ProcessMeta(httpContext);
				}
				else
				{
					await ProcessOperation(httpContext, serviceProvider);
				}
			}
			else
			{
				await _next(httpContext);
			}
		}

		private Message ProcessMeta(HttpContext httpContext)
		{
			MessageEncoder messageEncoder = _messageEncoders[0];
			string baseUrl = httpContext.Request.Scheme + "://" + httpContext.Request.Host + httpContext.Request.PathBase + httpContext.Request.Path;

			var bodyWriter = _serializer == SoapSerializer.XmlSerializer ? new MetaBodyWriter(_service, baseUrl) : (BodyWriter)new MetaWCFBodyWriter(_service, baseUrl, _binding);

			var responseMessage = Message.CreateMessage(messageEncoder.MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, _service, _binding);

			httpContext.Response.ContentType = messageEncoder.ContentType;
			messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);

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

			//Return metadata if no request
			if (httpContext.Request.Body.Length == 0)
			{
				return ProcessMeta(httpContext);
			}

			// Get the encoder based on Content Type
			var messageEncoder = _messageEncoders[0];
			for (int i = 0; i < _messageEncoders.Length; i++)
			{
				if (_messageEncoders[i].IsContentTypeSupported(httpContext.Request.ContentType))
				{
					messageEncoder = _messageEncoders[i];
					break;
				}
			}

			//Get the message
			var requestMessage = messageEncoder.ReadMessage(httpContext.Request.Body, 0x10000, httpContext.Request.ContentType);

			// Get MessageFilters, ModelBindingFilters
			var messageFilters = serviceProvider.GetServices<IMessageFilter>();
			var modelBindingFilters = serviceProvider.GetServices<IModelBindingFilter>();

			// Execute request message filters
			try
			{
				foreach (var messageFilter in messageFilters)
				{
					messageFilter.OnRequestExecuting(requestMessage);
				}
			}
			catch (Exception ex)
			{
				responseMessage = WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, messageEncoder, httpContext);
				return responseMessage;
			}

			var messageInspector = serviceProvider.GetService<IMessageInspector>();
			var correlationObject = messageInspector?.AfterReceiveRequest(ref requestMessage);

			var messageInspector2s = serviceProvider.GetServices<IMessageInspector2>();
#pragma warning disable SA1008 // StyleCop has not yet been updated to support tuples
			var correlationObjects2 = messageInspector2s.Select(mi => (inspector: mi, correlationObject: mi.AfterReceiveRequest(ref requestMessage, _service))).ToList();
#pragma warning restore SA1008

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
					var resultName = operation.ReturnName;
					var bodyWriter = new ServiceBodyWriter(_serializer, operation, resultName, responseObject, resultOutDictionary);
					responseMessage = Message.CreateMessage(messageEncoder.MessageVersion, null, bodyWriter);
					responseMessage = new CustomMessage(responseMessage);

					httpContext.Response.ContentType = httpContext.Request.ContentType;
					httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;

					correlationObjects2.ForEach(mi => mi.inspector.BeforeSendReply(ref responseMessage, _service, mi.correlationObject));

					messageInspector?.BeforeSendReply(ref responseMessage, correlationObject);

					messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);
				}
				catch (Exception exception)
				{
					_logger.LogWarning(0, exception, exception.Message);
					responseMessage = WriteErrorResponseMessage(exception, StatusCodes.Status500InternalServerError, serviceProvider, messageEncoder, httpContext);
				}
			}

			// Execute response message filters
			try
			{
				foreach (var messageFilter in messageFilters)
				{
					messageFilter.OnResponseExecuting(responseMessage);
				}
			}
			catch (Exception ex)
			{
				responseMessage = WriteErrorResponseMessage(ex, StatusCodes.Status500InternalServerError, serviceProvider, messageEncoder, httpContext);
				return responseMessage;
			}

			return responseMessage;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private object[] GetRequestArguments(Message requestMessage, System.Xml.XmlDictionaryReader xmlReader, OperationDescription operation, HttpContext httpContext)
		{
			var arguments = new object[operation.AllParameters.Length];

			// Find the element for the operation's data
			if (!operation.IsMessageContractRequest)
			{
				xmlReader.ReadStartElement(operation.Name, operation.Contract.Namespace);
			}

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

			foreach (var parameterInfo in operation.InParameters)
			{
				var parameterName = parameterInfo.Name;

				var parameterNs = parameterInfo.Namespace ?? operation.Contract.Namespace;

				if (xmlReader.IsStartElement(parameterName, parameterNs))
				{
					xmlReader.MoveToStartElement(parameterName, parameterNs);

					if (xmlReader.IsStartElement(parameterName, parameterNs))
					{
						switch (_serializer)
						{
							case SoapSerializer.XmlSerializer:
								{
									// case [XmlElement("parameter")] int parameter
									// case int[] parameter
									// case [XmlArray("parameter")] int[] parameter
									if (!parameterInfo.Parameter.ParameterType.IsArray || (parameterInfo.ArrayName != null && parameterInfo.ArrayItemName == null))
									{
										// see https://referencesource.microsoft.com/System.Xml/System/Xml/Serialization/XmlSerializer.cs.html#c97688a6c07294d5
										var elementType = parameterInfo.Parameter.ParameterType.GetElementType();
										if (elementType == null || parameterInfo.Parameter.ParameterType.IsArray)
										{
											elementType = parameterInfo.Parameter.ParameterType;
										}

										var serializer = CachedXmlSerializer.GetXmlSerializer(elementType, parameterName, parameterNs);
										lock (serializer)
										{
											arguments[parameterInfo.Index] = serializer.Deserialize(xmlReader);
										}
									}

									// case [XmlElement("parameter")] int[] parameter
									// case [XmlArray("parameter"), XmlArrayItem(ElementName = "item")] int[] parameter
									else
									{
										//if (parameterInfo.ArrayItemName != null)
										{
											xmlReader.ReadStartElement(parameterName, parameterNs);
										}

										var elementType = parameterInfo.Parameter.ParameterType.GetElementType();

										var localName = parameterInfo.ArrayItemName ?? elementType.Name;
										if (parameterInfo.ArrayItemName == null && elementType.Namespace.StartsWith("System"))
										{
											localName = localName.ToLower();
										}

										//localName = "ComplexModelInput";
										var deserializeMethod = typeof(XmlSerializerExtensions)
											.GetGenericMethod(nameof(XmlSerializerExtensions.DeserializeArray), new[] { elementType });
										var serializer = CachedXmlSerializer.GetXmlSerializer(elementType, localName, parameterNs);
										lock (serializer)
										{
											arguments[parameterInfo.Index] = deserializeMethod.Invoke(null, new object[] { serializer, localName, parameterNs, xmlReader });
										}

										//if (parameterInfo.ArrayItemName != null)
										{
											xmlReader.ReadEndElement();
										}
									}
								}

								break;
							case SoapSerializer.DataContractSerializer:
								{
									var elementType = parameterInfo.Parameter.ParameterType.GetElementType();
									if (elementType == null || parameterInfo.Parameter.ParameterType.IsArray)
									{
										elementType = parameterInfo.Parameter.ParameterType;
									}

									var serializer = new DataContractSerializer(elementType, parameterName, parameterNs);
									arguments[parameterInfo.Index] = serializer.ReadObject(xmlReader, verifyObjectName: true);
								}

								break;
							default: throw new NotImplementedException();
						}
					}
				}
				else if (parameterInfo.Parameter.ParameterType == typeof(HttpContext))
				{
					arguments[parameterInfo.Index] = httpContext;
				}
				else
				{
					arguments[parameterInfo.Index] = null;
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
		/// <param name="messageEncoder">
		/// The Message Encoder.
		/// </param>
		/// <param name="httpContext">
		/// The HTTP context that received the response message.
		/// </param>
		/// <returns>
		/// Returns the constructed message (which is implicitly written to the response
		/// and therefore must not be handled by the caller).
		/// </returns>
		private Message WriteErrorResponseMessage(
			Exception exception,
			int statusCode,
			IServiceProvider serviceProvider,
			MessageEncoder messageEncoder,
			HttpContext httpContext)
		{
			// Create response message
			object faultDetail = ExtractFaultDetail(exception);
			string errorText = exception.InnerException != null ? exception.InnerException.Message : exception.Message;
			var transformer = serviceProvider.GetService<ExceptionTransformer>();
			if (transformer != null)
			{
				errorText = transformer.Transform(exception);
			}

			var bodyWriter = new FaultBodyWriter(new Fault(faultDetail) { FaultString = errorText });
			var responseMessage = Message.CreateMessage(messageEncoder.MessageVersion, null, bodyWriter);
			responseMessage = new CustomMessage(responseMessage);

			httpContext.Response.ContentType = httpContext.Request.ContentType;
			httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;
			httpContext.Response.StatusCode = statusCode;
			messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);

			return responseMessage;
		}

		/// <summary>
		/// Helper to extract object of a detailed fault.
		/// </summary>
		/// <param name="exception">
		/// The exception that caused the failure.
		/// </param>
		/// <returns>
		/// Returns instance of T if the exception (or its InnerExceptions) is of type FaultException<T>
		/// otherwise returns null
		/// </returns>
		private object ExtractFaultDetail(Exception exception)
		{
			try
			{
				Exception currentException = exception;
				while (currentException != null)
				{
					var type = currentException.GetType();
					if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(FaultException<>))
					{
						var detailInfo = type.GetProperty("Detail");
						var value = detailInfo?.GetValue(currentException);

						if (value != null)
						{
							return value;
						}
					}

					currentException = currentException.InnerException;
				}
			}
			catch
			{
				return null;
			}

			return null;
		}
	}
}
