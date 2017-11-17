using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
			Console.WriteLine($"Request for {httpContext.Request.Path} received ({httpContext.Request.ContentLength ?? 0} bytes)");
			if (httpContext.Request.Path.Equals(_endpointPath, StringComparison.Ordinal))
			{
				Message responseMessage;

				if (httpContext.Request.Query.ContainsKey("wsdl"))
				{
					responseMessage = ProcessMeta(httpContext, serviceProvider);
				}
				else
				{
					responseMessage = await ProcessOperation(httpContext, serviceProvider);
				}
			}
			else
			{
				await _next(httpContext);
			}
		}

		private Message ProcessMeta(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			Message responseMessage = null;

			string baseUrl = httpContext.Request.Scheme + "://" + httpContext.Request.Host.ToString() + httpContext.Request.PathBase + httpContext.Request.Path;

			var bodyWriter = new MetaBodyWriter(_service, baseUrl);

			responseMessage = Message.CreateMessage(_messageEncoder.MessageVersion, null, bodyWriter);
			responseMessage = new MetaMessage(responseMessage, _service);

			httpContext.Response.ContentType = _messageEncoder.ContentType;
			_messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);

			return responseMessage;
		}

		private async Task<Message> ProcessOperation(HttpContext httpContext, IServiceProvider serviceProvider)
		{
			Message responseMessage;

			// Read request message
			var requestMessage = _messageEncoder.ReadMessage(httpContext.Request.Body, 0x10000, httpContext.Request.ContentType);

			var soapAction = (httpContext.Request.Headers["SOAPAction"].FirstOrDefault() ?? requestMessage.GetReaderAtBodyContents().LocalName).Trim('\"');
			if (!string.IsNullOrEmpty(soapAction))
			{
				requestMessage.Headers.Action = soapAction;
			}

			var operation =
				_service.Operations.FirstOrDefault(
					o => o.SoapAction.Equals(soapAction, StringComparison.Ordinal) || o.Name.Equals(soapAction, StringComparison.Ordinal));
			if (operation == null)
			{
				throw new InvalidOperationException($"No operation found for specified action: {requestMessage.Headers.Action}");
			}
			// Get service type
			var serviceInstance = serviceProvider.GetService(_service.ServiceType);

			// Get operation arguments from message
			Dictionary<string, object> outArgs = new Dictionary<string, object>();
			var arguments = GetRequestArguments(requestMessage, operation, ref outArgs);
			var allArgs = arguments.Concat(outArgs.Values).ToArray();
			// Invoke Operation method

			try
			{
				var responseObject = operation.DispatchMethod.Invoke(serviceInstance, allArgs);
				var responseType = responseObject.GetType();
				if (responseType.IsConstructedGenericType && responseType.GetGenericTypeDefinition() == typeof(Task<>))
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
				var errorText = exception.InnerException.Message;
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
					var parameterName = parameters[i].GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameters[i].Name;
					xmlReader.MoveToStartElement(parameterName, operation.Contract.Namespace);
					if (xmlReader.IsStartElement(parameterName, operation.Contract.Namespace))
					{
						var elementType = parameters[i].ParameterType.GetElementType();
						if (elementType == null || parameters[i].ParameterType.IsArray)
							elementType = parameters[i].ParameterType;
						string objectNamespace = operation.Contract.Namespace;

						var serializer = new DataContractSerializer(elementType, parameterName, objectNamespace);
						arguments.Add(serializer.ReadObject(xmlReader, verifyObjectName: true));
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
