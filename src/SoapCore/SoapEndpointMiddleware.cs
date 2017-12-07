using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace SoapCore
{
	public class SoapEndpointMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ServiceDescription _service;
		private readonly string _endpointPath;
		private readonly MessageEncoder _messageEncoder;

		public SoapEndpointMiddleware(RequestDelegate next, Type serviceType, string path, MessageEncoder encoder)
		{
			_next = next;
			_endpointPath = path;
			_messageEncoder = encoder;
			_service = new ServiceDescription(serviceType);
		}

		public async Task Invoke(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			httpContext.Request.EnableRewind();
			Console.WriteLine($"Request for {httpContext.Request.Path} received ({httpContext.Request.ContentLength ?? 0} bytes)");
			if (httpContext.Request.Path.Equals(_endpointPath, StringComparison.Ordinal))
			{
				if (httpContext.Request.Query.ContainsKey("wsdl"))
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
			string baseUrl = httpContext.Request.Scheme + "://" + httpContext.Request.Host + httpContext.Request.PathBase + httpContext.Request.Path;

			var bodyWriter = new MetaBodyWriter(_service, baseUrl);

			var responseMessage = Message.CreateMessage(_messageEncoder.MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, _service);

			httpContext.Response.ContentType = _messageEncoder.ContentType;
			_messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);

			return responseMessage;
		}

		private async Task<Message> ProcessOperation(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			Message responseMessage;

			//Reload the body to ensure we have the full message
			var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
			var requestData = Encoding.UTF8.GetBytes(body);
			httpContext.Request.Body = new MemoryStream(requestData);

			//Return metadata if no request
			if (httpContext.Request.Body.Length == 0)
				return ProcessMeta(httpContext);

			//Get the message
			var requestMessage = _messageEncoder.ReadMessage(httpContext.Request.Body, 0x10000, httpContext.Request.ContentType);

			var soapAction = (httpContext.Request.Headers["SOAPAction"].FirstOrDefault() ?? requestMessage.GetReaderAtBodyContents().LocalName).Trim('\"');
			if (!string.IsNullOrEmpty(soapAction))
			{
				requestMessage.Headers.Action = soapAction;
			}

			var operation = _service.Operations.FirstOrDefault(o => o.SoapAction.Equals(soapAction, StringComparison.Ordinal) || o.Name.Equals(soapAction, StringComparison.Ordinal));
			if (operation == null)
			{
				throw new InvalidOperationException($"No operation found for specified action: {requestMessage.Headers.Action}");
			}

			try
			{
				//Create an instance of the service class
				var serviceInstance = serviceProvider.GetService(_service.ServiceType);

				// Get operation arguments from message
				Dictionary<string, object> outArgs = new Dictionary<string, object>();
				var arguments = GetRequestArguments(requestMessage, operation, ref outArgs);
				var allArgs = arguments.Concat(outArgs.Values).ToArray();

				// Invoke Operation method
				var responseObject = operation.DispatchMethod.Invoke(serviceInstance, allArgs);
				if (operation.DispatchMethod.ReturnType.IsConstructedGenericType && operation.DispatchMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
				{
					var responseTask = (Task)responseObject;
					await responseTask;
					responseObject = responseTask.GetType().GetProperty("Result").GetValue(responseTask);
				}
				int i = arguments.Length;
				var resultOutDictionary = new Dictionary<string, object>();
				foreach (var outArg in outArgs)
				{
					resultOutDictionary[outArg.Key] = allArgs[i];
					i++;
				}

				// Create response message
				var resultName = operation.DispatchMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? operation.Name + "Result";
				var bodyWriter = new ServiceBodyWriter(operation.Contract.Namespace, operation.Name + "Response", resultName, responseObject, resultOutDictionary);
				responseMessage = Message.CreateMessage(_messageEncoder.MessageVersion, null, bodyWriter);
				responseMessage = new CustomMessage(responseMessage);

				httpContext.Response.ContentType = httpContext.Request.ContentType; // _messageEncoder.ContentType;
				httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;

				_messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);
			}
			catch (Exception exception)
			{
				// Create response message
				while (exception.InnerException != null)
					exception = exception.InnerException;

				var errorText = exception.Message;

				var transformer = serviceProvider.GetService<ExceptionTransformer>();
				if (transformer != null)
					errorText = transformer.Transform(exception.InnerException);
				var bodyWriter = new FaultBodyWriter(new Fault { FaultString = errorText });
				responseMessage = Message.CreateMessage(_messageEncoder.MessageVersion, null, bodyWriter);
				responseMessage = new CustomMessage(responseMessage);

				httpContext.Response.ContentType = httpContext.Request.ContentType; // _messageEncoder.ContentType;
				httpContext.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;
				httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
				_messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);
			}

			return responseMessage;
		}

		private object[] GetRequestArguments(Message requestMessage, OperationDescription operation, ref Dictionary<string, object> outArgs)
		{
			var parameters = operation.DispatchMethod.GetParameters().Where(x => !x.IsOut && !x.ParameterType.IsByRef).ToArray();
			var arguments = new List<object>();

			// Deserialize request wrapper and object
			using (var xmlReader = requestMessage.GetReaderAtBodyContents())
			{
				// Find the element for the operation's data
				xmlReader.ReadStartElement(operation.Name, operation.Contract.Namespace);

				for (int i = 0; i < parameters.Length; i++)
				{
					var elementAttribute = parameters[i].GetCustomAttribute<XmlElementAttribute>();
					var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
						                    ? elementAttribute.ElementName
						                    : parameters[i].GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameters[i].Name;
					var parameterNs = elementAttribute?.Namespace ?? operation.Contract.Namespace;

					if (xmlReader.IsStartElement(parameterName, parameterNs))
					{
						xmlReader.MoveToStartElement(parameterName, parameterNs);

						if (xmlReader.IsStartElement(parameterName, parameterNs))
						{
							var elementType = parameters[i].ParameterType.GetElementType();
							if (elementType == null || parameters[i].ParameterType.IsArray)
								elementType = parameters[i].ParameterType;

							var serializer = new DataContractSerializer(elementType, parameterName, parameterNs);
							arguments.Add(serializer.ReadObject(xmlReader, verifyObjectName: true));
						}
					}
					else
					{
						arguments.Add(null);
					}
				}
			}

			var outParams = operation.DispatchMethod.GetParameters().Where(x => x.IsOut || x.ParameterType.IsByRef).ToArray();
			foreach (var parameterInfo in outParams)
			{
				if (parameterInfo.ParameterType.Name == "Guid&")
					outArgs[parameterInfo.Name] = Guid.Empty;
				else if (parameterInfo.ParameterType.Name == "String&" || parameterInfo.ParameterType.GetElementType().IsArray)
					outArgs[parameterInfo.Name] = null;
				else
				{
					var type = parameterInfo.ParameterType.GetElementType();
					outArgs[parameterInfo.Name] = Activator.CreateInstance(type);
				}
			}
			return arguments.ToArray();
		}
	}
}
