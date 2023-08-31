using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

namespace RBC.VifInterfaceWebServices.Middlewares
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        public RequestResponseLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<RequestResponseLoggingMiddleware>();
            _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
        }

        public async Task Invoke(HttpContext context)
        {
            await LogRequest(context);
            await LogResponse(context);
		}

        private static string ReadStreamInChunks(Stream stream)
		{
			const int readChunkBufferLength = 4096;
			stream.Seek(0, SeekOrigin.Begin);
			using var textWriter = new StringWriter();
			using var reader = new StreamReader(stream);
			var readChunk = new char[readChunkBufferLength];
			int readChunkLength;
			do
			{
				readChunkLength = reader.ReadBlock(readChunk, 0, readChunkBufferLength);
				textWriter.Write(readChunk, 0, readChunkLength);
			}
			while (readChunkLength > 0);
			return textWriter.ToString();
		}

        private async Task LogResponse(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;
            await using var responseBody = _recyclableMemoryStreamManager.GetStream();
            context.Response.Body = responseBody;
            await _next(context);
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            string information = $"{Environment.NewLine}=========================================================================================={Environment.NewLine}" +
                                 $"Date: {DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fffff")}{Environment.NewLine}" +
                                 $"Http Response Information: {Environment.NewLine}" +
                                 $"Schema: {context.Request.Scheme} {Environment.NewLine}" +
                                 $"Host: {context.Request.Host} {Environment.NewLine}" +
                                 $"Path: {context.Request.Path} {Environment.NewLine}" +
                                 $"QueryString: {context.Request.QueryString} {Environment.NewLine}" +
                                 $"Http Status Code: {context.Response.StatusCode} {Environment.NewLine}" +
                                 $"Response Body: {text}{Environment.NewLine}" +
                                 $"=========================================================================================={Environment.NewLine}";
            Debug.WriteLine(information);
            _logger.LogInformation(information);
            await responseBody.CopyToAsync(originalBodyStream);
        }

        private async Task LogRequest(HttpContext context)
        {
            context.Request.EnableBuffering();
            StringBuilder headerInformation = new StringBuilder();
            foreach (var element in context.Request.Headers)
            {
                headerInformation.AppendLine($"Header: {element.Key} -> {element.Value}");
            }

            await using var requestStream = _recyclableMemoryStreamManager.GetStream();
            await context.Request.Body.CopyToAsync(requestStream);
            StringBuilder information = new StringBuilder();
            information.Append($"{Environment.NewLine}=========================================================================================={Environment.NewLine}");
            information.Append($"Date: {DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fffff")}{Environment.NewLine}");
            information.Append($"IP: {context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}{Environment.NewLine}");
            information.Append($"Method: {context.Request.Method}{Environment.NewLine}");
            information.Append($"Http Request Information: {Environment.NewLine}");
            information.Append(headerInformation);
            information.Append($"Schema: {context.Request.Scheme} {Environment.NewLine}");
            information.Append($"Host: {context.Request.Host} {Environment.NewLine}");
            information.Append($"Path: {context.Request.Path} {Environment.NewLine}");
            information.Append($"QueryString: {context.Request.QueryString} {Environment.NewLine}");
            information.Append($"Request Body: {ReadStreamInChunks(requestStream)}{Environment.NewLine}");
            information.Append($"=========================================================================================={Environment.NewLine}");
            _logger.LogInformation(information.ToString());
            Debug.WriteLine(information);
            context.Request.Body.Position = 0;
        }
    }
}
