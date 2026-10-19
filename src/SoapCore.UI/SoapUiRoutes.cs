using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace SoapCore.UI
{
	/// <summary>
	/// The route table of the explorer and its handlers. Only GET and HEAD are mapped, every route is a literal string,
	/// and handlers write the response directly so that every header stays under our control.
	/// </summary>
	internal sealed class SoapUiRoutes
	{
		private const string AnonymousWarning =
			"SoapCore.UI is being served without authorization metadata outside the Development environment. Anyone who can reach this host can list and call your SOAP endpoints through the explorer. Chain .RequireAuthorization() on MapSoapCoreUi().";

		private static readonly string[] Methods = { HttpMethods.Get, HttpMethods.Head };

		// The routes of the embedded files other than the index. A fixed list of literals: nothing here is ever derived from request input,
		// and every route of it must have a matching entry in SoapUiAssetStore.
		private static readonly string[] AssetRoutes =
		{
			"/app.js",
			"/wsdl-parser.js",
			"/form-editor.js",
			"/xml-view.js",
			"/style.css",
			"/form-editor.css",
		};

		// Nulls are emitted explicitly. The default encoder is kept on purpose: it escapes < > & ' and non-ASCII characters.
		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
			WriteIndented = false,
		};

		private readonly SoapUiCatalog _catalog;
		private readonly IReadOnlyDictionary<string, SoapUiAsset> _assets;
		private readonly ILogger _logger;
		private readonly string _redirectSegment;
		private int _anonymousWarned;

		public SoapUiRoutes(SoapUiCatalog catalog, IReadOnlyDictionary<string, SoapUiAsset> assets, ILogger logger, string redirectSegment)
		{
			_catalog = catalog;
			_assets = assets;
			_logger = logger;
			_redirectSegment = redirectSegment;
		}

		public void Map(IEndpointRouteBuilder group)
		{
			var index = _assets["/"];

			// Typed locals, so that overload resolution picks the RequestDelegate overload of MapMethods.
			RequestDelegate root = context => HandleRoot(context, index);
			RequestDelegate api = HandleApi;

			// "/" answers both {pattern} and {pattern}/, because routing treats them as one endpoint.
			group.MapMethods("/", Methods, root);

			foreach (var route in AssetRoutes)
			{
				var asset = _assets[route];
				RequestDelegate handler = context => HandleAsset(context, asset);
				group.MapMethods(route, Methods, handler);
			}

			group.MapMethods("/api/services", Methods, api);
		}

		private static string EscapeQuery(string? query)
		{
			if (string.IsNullOrEmpty(query))
			{
				return string.Empty;
			}

			// Everything outside U+0021..U+007E is percent-encoded as UTF-8, so no control character or space reaches the header.
			var builder = new StringBuilder(query.Length + 8);
			for (var i = 0; i < query.Length; i++)
			{
				var c = query[i];
				if (c >= '!' && c <= '~')
				{
					builder.Append(c);
					continue;
				}

				string text;
				if (char.IsHighSurrogate(c) && i + 1 < query.Length && char.IsLowSurrogate(query[i + 1]))
				{
					text = query.Substring(i, 2);
					i++;
				}
				else if (char.IsSurrogate(c))
				{
					text = new string((char)0xFFFD, 1);
				}
				else
				{
					text = new string(c, 1);
				}

				foreach (var b in Encoding.UTF8.GetBytes(text))
				{
					builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
				}
			}

			return builder.ToString();
		}

		private static bool IsCrossOriginRequest(HttpContext context)
		{
			// The explorer is same-origin by design, and its own requests never carry an Origin header: browsers only add one to
			// GET and HEAD requests that are cross-origin. Such a request is refused, so a permissive host-wide CORS policy
			// (UseCors() with a default policy) cannot make the explorer readable from another site.
			// Chaining RequireCors()/EnableCors on MapSoapCoreUi() puts CORS metadata on the endpoint: that is an explicit opt-in.
			// DisableCors metadata is not used for this: any CORS metadata makes the endpoint middleware throw when UseCors() is absent.
			var origin = context.Request.Headers[HeaderNames.Origin];
			if (StringValues.IsNullOrEmpty(origin))
			{
				return false;
			}

			if (context.GetEndpoint()?.Metadata.GetMetadata<ICorsMetadata>() != null)
			{
				return false;
			}

			var request = context.Request;
			var ownOrigin = request.Scheme + "://" + request.Host.Value;
			return !string.Equals(origin.ToString(), ownOrigin, StringComparison.OrdinalIgnoreCase);
		}

		private static bool HasAuthorization(HttpContext context)
		{
			var metadata = context.GetEndpoint()?.Metadata;
			if (metadata == null)
			{
				return false;
			}

			// RequireAuthorization() and [Authorize] put IAuthorizeData on the endpoint.
			if (metadata.GetMetadata<IAuthorizeData>() != null)
			{
				return true;
			}

			// A FallbackPolicy protects every endpoint that carries no authorization metadata, unless it opts out with AllowAnonymous.
			var fallbackPolicy = context.RequestServices.GetService<IOptions<AuthorizationOptions>>()?.Value.FallbackPolicy;
			return fallbackPolicy != null && metadata.GetMetadata<IAllowAnonymous>() == null;
		}

		private static bool IsNotModified(HttpRequest request, EntityTagHeaderValue etag)
		{
			foreach (var candidate in request.GetTypedHeaders().IfNoneMatch)
			{
				if (candidate.Equals(EntityTagHeaderValue.Any) || etag.Compare(candidate, useStrongComparison: false))
				{
					return true;
				}
			}

			return false;
		}

		private static async Task WriteAsset(HttpContext context, SoapUiAsset asset)
		{
			var request = context.Request;
			var response = context.Response;

			SoapUiResponseHeaders.Apply(response, noStore: false);
			response.GetTypedHeaders().ETag = asset.ETag;

			if (IsNotModified(request, asset.ETag))
			{
				response.StatusCode = StatusCodes.Status304NotModified;
				return;
			}

			response.ContentType = asset.ContentType;
			response.ContentLength = asset.Bytes.Length;

			if (!HttpMethods.IsHead(request.Method))
			{
				await response.Body.WriteAsync(asset.Bytes, context.RequestAborted);
			}
		}

		private Task HandleRoot(HttpContext context, SoapUiAsset index)
		{
			if (Begin(context))
			{
				return Task.CompletedTask;
			}

			// {pattern} and {pattern}/ reach this handler. Only the one without the trailing slash is redirected.
			var path = context.Request.Path.Value;
			if (string.IsNullOrEmpty(path) || path[path.Length - 1] != '/')
			{
				Redirect(context);
				return Task.CompletedTask;
			}

			return WriteAsset(context, index);
		}

		private Task HandleAsset(HttpContext context, SoapUiAsset asset)
		{
			if (Begin(context))
			{
				return Task.CompletedTask;
			}

			return WriteAsset(context, asset);
		}

		private async Task HandleApi(HttpContext context)
		{
			if (Begin(context))
			{
				return;
			}

			var catalog = _catalog.Build(context.Request.PathBase);
			var body = JsonSerializer.SerializeToUtf8Bytes(catalog, JsonOptions);

			var response = context.Response;
			SoapUiResponseHeaders.Apply(response, noStore: true);
			response.ContentType = "application/json; charset=utf-8";
			response.ContentLength = body.Length;

			if (!HttpMethods.IsHead(context.Request.Method))
			{
				await response.Body.WriteAsync(body, context.RequestAborted);
			}
		}

		// Runs what every handler starts with. Returns true when the request was refused and the handler must stop.
		private bool Begin(HttpContext context)
		{
			WarnIfAnonymous(context);

			if (!IsCrossOriginRequest(context))
			{
				return false;
			}

			var response = context.Response;
			SoapUiResponseHeaders.Apply(response, noStore: true);
			response.StatusCode = StatusCodes.Status403Forbidden;
			response.ContentLength = 0;
			return true;
		}

		private void Redirect(HttpContext context)
		{
			var response = context.Response;
			SoapUiResponseHeaders.Apply(response, noStore: false);

			// A relative Location built from the configured last segment of the pattern and the escaped query only.
			// Nothing from the request path or the Host header is used, so it works under any PathBase or proxy prefix.
			response.StatusCode = StatusCodes.Status308PermanentRedirect;
			response.Headers[HeaderNames.Location] = _redirectSegment + "/" + EscapeQuery(context.Request.QueryString.Value);
			response.ContentLength = 0;
		}

		private void WarnIfAnonymous(HttpContext context)
		{
			if (Volatile.Read(ref _anonymousWarned) != 0)
			{
				return;
			}

			var environment = context.RequestServices.GetService<IHostEnvironment>() ?? context.RequestServices.GetService<IWebHostEnvironment>();
			if (environment == null || environment.IsDevelopment())
			{
				return;
			}

			if (HasAuthorization(context))
			{
				return;
			}

			if (Interlocked.Exchange(ref _anonymousWarned, 1) == 0)
			{
				_logger.LogWarning(AnonymousWarning);
			}
		}
	}
}
