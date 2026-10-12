using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoapCore.Extensibility;
using SoapCore.ServiceModel;

namespace SoapCore.UI
{
	/// <summary>
	/// Turns the endpoints recorded by <see cref="ISoapEndpointRegistry"/> into the DTOs served by the explorer API.
	/// The registry is read on every request, so endpoints registered later show up at once and any <see cref="ISoapEndpointRegistry"/>
	/// implementation works. Only the expensive part, describing a service type, is cached (per service type).
	/// The cache does not depend on <see cref="SoapUiOptions.ExposeOperationDetails"/>: both the detailed and the plain operations are kept,
	/// and each request picks one, so changing the option can never expose details that were not meant to be shown.
	/// The descriptions (service, operation, and parameter in the detailed version) are resolved by <see cref="SoapUiDocumentationProvider"/> when a
	/// service type is first described and cached with the operations, so the reflection and the XML documentation are read once per service type.
	/// Nothing is read in the constructor.
	/// </summary>
	internal sealed class SoapUiCatalog
	{
		private const string NoServiceTypeError = "The endpoint has no service type.";
		private const string OperationsFailedError = "Could not read the operations of this service.";
		private const string EndpointFailedError = "Could not describe this endpoint.";

		// Caps that keep the response small for huge contracts.
		private const int MaxParametersPerOperation = 100;
		private const int MaxFaultsPerOperation = 20;

		private const string VoidTypeName = "void";

		private readonly ISoapEndpointRegistry _registry;
		private readonly IOptions<SoapUiOptions> _options;
		private readonly SoapUiDocumentationProvider _documentation;
		private readonly ILogger<SoapUiCatalog> _logger;

		// One cache per value of GenerateSoapActionWithoutContractName. ServiceDescription is costly, so it is built once per service type.
		private readonly ConcurrentDictionary<Type, Lazy<OperationsResult>> _operationsWithContractName =
			new ConcurrentDictionary<Type, Lazy<OperationsResult>>();

		private readonly ConcurrentDictionary<Type, Lazy<OperationsResult>> _operationsWithoutContractName =
			new ConcurrentDictionary<Type, Lazy<OperationsResult>>();

		public SoapUiCatalog(ISoapEndpointRegistry registry, IOptions<SoapUiOptions> options, SoapUiDocumentationProvider documentation, ILogger<SoapUiCatalog> logger)
		{
			_registry = registry;
			_options = options;
			_documentation = documentation;
			_logger = logger;
		}

		public SoapUiCatalogDto Build(PathString pathBase)
		{
			var options = _options.Value;

			var prepared = Prepare(_registry.GetAll() ?? Array.Empty<SoapEndpointInfo>(), options.ExposeOperationDetails);

			// PathBase and the prefix are joined per request and never used as a cache key.
			var prefix = string.IsNullOrEmpty(options.EndpointPathPrefix) ? PathString.Empty : new PathString(options.EndpointPathPrefix);
			var root = pathBase.Add(prefix);

			var services = new List<SoapUiServiceDto>(prepared.Count);
			foreach (var service in prepared)
			{
				var full = service.NormalizedPath == "/" ? root : root.Add(new PathString(service.NormalizedPath));
				var urlPath = full.HasValue ? full.ToUriComponent() : "/";

				services.Add(new SoapUiServiceDto
				{
					Id = service.Id,
					Name = service.Name,
					Path = urlPath,
					WsdlUrl = urlPath + "?wsdl",
					ServiceType = service.ServiceType,
					Serializer = service.Serializer,
					CustomSerializerType = service.CustomSerializerType,
					SoapVersions = service.SoapVersions,
					CaseInsensitivePath = service.CaseInsensitivePath,
					Description = service.Description,
					Operations = service.Operations,
					OperationsError = service.OperationsError,
				});
			}

			return new SoapUiCatalogDto
			{
				Title = options.Title,
				Description = string.IsNullOrWhiteSpace(options.Description) ? null : options.Description,
				TryItOutEnabled = options.EnableTryItOut,
				OperationDetailsEnabled = options.ExposeOperationDetails,
				BasePath = pathBase.HasValue ? pathBase.ToUriComponent() : string.Empty,
				DefaultSoapVersion = MapDefaultSoapVersion(options.DefaultSoapVersion),
				DocExpansion = MapDocExpansion(options.DocExpansion),
				AuthSchemes = MapAuthSchemes(options.AuthSchemes),
				ApiKeyHeaderName = options.ApiKeyHeaderName,
				StylesheetUrl = MapStylesheetUrl(options.StylesheetUrl),
				FormEditorEnabled = options.EnableFormEditor,
				SendTimeoutSeconds = MapSendTimeoutSeconds(options.SendTimeoutSeconds),
				MaxResponseBytes = MapMaxResponseBytes(options.MaxResponseBytes),
				UiVersion = SoapUiVersion.Current,
				Services = services,
			};
		}

		private static string? MapDefaultSoapVersion(string? version)
		{
			// Validated when the UI is mapped. Anything else, which only a change made after that could produce, means automatic.
			var known = string.Equals(version, "1.1", StringComparison.Ordinal) || string.Equals(version, "1.2", StringComparison.Ordinal);
			return known ? version : null;
		}

		private static string MapDocExpansion(SoapUiDocExpansion docExpansion)
		{
			return docExpansion == SoapUiDocExpansion.Full ? "full" : "list";
		}

		private static string[] MapAuthSchemes(SoapUiAuthSchemes schemes)
		{
			var names = new List<string>(4);
			if ((schemes & SoapUiAuthSchemes.Bearer) != 0)
			{
				names.Add("bearer");
			}

			if ((schemes & SoapUiAuthSchemes.Basic) != 0)
			{
				names.Add("basic");
			}

			if ((schemes & SoapUiAuthSchemes.ApiKey) != 0)
			{
				names.Add("apiKey");
			}

			if ((schemes & SoapUiAuthSchemes.WsUsernameToken) != 0)
			{
				names.Add("wsUsernameToken");
			}

			return names.ToArray();
		}

		private static string? MapStylesheetUrl(string? url)
		{
			// The page links this URL, so it is checked again on the way out: only a same-origin path is ever served.
			return SoapUiPathRules.IsValidStylesheetUrl(url) ? url : null;
		}

		private static int MapSendTimeoutSeconds(int seconds)
		{
			// Validated when the UI is mapped. Anything else, which only a change made after that could produce, means the default.
			return SoapUiLimits.IsValidSendTimeoutSeconds(seconds) ? seconds : SoapUiLimits.DefaultSendTimeoutSeconds;
		}

		private static int MapMaxResponseBytes(int bytes)
		{
			return SoapUiLimits.IsValidMaxResponseBytes(bytes) ? bytes : SoapUiLimits.DefaultMaxResponseBytes;
		}

		private static string NormalizePath(string? path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return "/";
			}

			var trimmed = path.Trim();
			if (trimmed[0] != '/')
			{
				trimmed = "/" + trimmed;
			}

			trimmed = trimmed.TrimEnd('/');
			return trimmed.Length == 0 ? "/" : trimmed;
		}

		private static string Slugify(string normalizedPath)
		{
			var builder = new StringBuilder(normalizedPath.Length);
			var pendingDash = false;
			foreach (var character in normalizedPath)
			{
				var c = char.ToLowerInvariant(character);
				if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
				{
					if (pendingDash && builder.Length > 0)
					{
						builder.Append('-');
					}

					builder.Append(c);
					pendingDash = false;
				}
				else
				{
					pendingDash = true;
				}
			}

			return builder.Length == 0 ? "service" : builder.ToString();
		}

		private static string UniqueId(string slug, ISet<string> used)
		{
			var candidate = slug;
			var n = 1;
			while (!used.Add(candidate))
			{
				n++;
				candidate = slug + "-" + n.ToString(CultureInfo.InvariantCulture);
			}

			return candidate;
		}

		private static IReadOnlyList<string> MapSoapVersions(IReadOnlyList<MessageVersion>? messageVersions)
		{
			if (messageVersions == null)
			{
				return Array.Empty<string>();
			}

			// MessageVersion.None and anything that is neither SOAP 1.1 nor SOAP 1.2 is ignored.
			var versions = new SortedSet<string>(StringComparer.Ordinal);
			foreach (var messageVersion in messageVersions)
			{
				if (messageVersion == null)
				{
					continue;
				}

				if (messageVersion.Envelope == EnvelopeVersion.Soap11)
				{
					versions.Add("1.1");
				}
				else if (messageVersion.Envelope == EnvelopeVersion.Soap12)
				{
					versions.Add("1.2");
				}
			}

			return versions.ToArray();
		}

		private static PreparedService CreateFallback(string id, string normalizedPath)
		{
			return new PreparedService
			{
				Id = id,
				NormalizedPath = normalizedPath,
				Name = normalizedPath,
				ServiceType = string.Empty,
				Serializer = string.Empty,
				CustomSerializerType = null,
				SoapVersions = Array.Empty<string>(),
				CaseInsensitivePath = false,
				Operations = Array.Empty<SoapUiOperationDto>(),
				OperationsError = EndpointFailedError,
			};
		}

		private static SoapUiOperationDto CreateOperation(OperationDescription operation, string? description, OperationDetails? details)
		{
			return new SoapUiOperationDto
			{
				Name = operation.Name,
				SoapAction = operation.SoapAction ?? string.Empty,
				IsOneWay = operation.IsOneWay,
				Contract = operation.Contract?.Name ?? string.Empty,
				ContractNamespace = operation.Contract?.Namespace ?? string.Empty,
				Description = description,
				Parameters = details?.Parameters,
				Returns = details?.Returns,
				Faults = details?.Faults,
				MessageContract = details?.MessageContract,
			};
		}

		private static OperationDetails ReadDetails(OperationDescription operation, Func<ParameterInfo, string?> describeParameter)
		{
			var parameters = new List<SoapUiParameterDto>();
			foreach (var parameter in operation.AllParameters ?? Array.Empty<SoapMethodParameterInfo>())
			{
				if (parameters.Count >= MaxParametersPerOperation)
				{
					break;
				}

				var parameterType = parameter.Parameter.ParameterType;
				if (IsInjectedParameter(parameterType))
				{
					continue;
				}

				parameters.Add(new SoapUiParameterDto
				{
					Name = parameter.Name ?? parameter.Parameter.Name ?? string.Empty,
					Type = FormatParameterType(parameterType),
					Direction = MapDirection(parameter.Direction),
					Description = describeParameter(parameter.Parameter),
				});
			}

			// Several operations often declare the same fault type, and a type may be declared twice: list each one once.
			var faults = new List<string>();
			foreach (var fault in operation.Faults ?? Array.Empty<Type>())
			{
				if (faults.Count >= MaxFaultsPerOperation)
				{
					break;
				}

				var faultName = SoapUiTypeNames.Format(fault);
				if (!faults.Contains(faultName, StringComparer.Ordinal))
				{
					faults.Add(faultName);
				}
			}

			return new OperationDetails(
				parameters,
				new SoapUiReturnDto { Type = FormatReturnType(operation) },
				faults,
				new SoapUiMessageContractDto { Request = operation.IsMessageContractRequest, Response = operation.IsMessageContractResponse });
		}

		// Not part of the SOAP message: SoapCore supplies these when it calls the service method (the WSDL skips CancellationToken too).
		private static bool IsInjectedParameter(Type parameterType)
		{
			return parameterType == typeof(CancellationToken) || parameterType == typeof(HttpContext);
		}

		private static string FormatParameterType(Type parameterType)
		{
			// out and ref parameters are reported with the type behind the reference, not "System.Int32&".
			return SoapUiTypeNames.Format(parameterType.IsByRef ? parameterType.GetElementType() ?? parameterType : parameterType);
		}

		private static string MapDirection(SoapMethodParameterDirection direction)
		{
			switch (direction)
			{
				case SoapMethodParameterDirection.OutOnlyRef:
					return "out";
				case SoapMethodParameterDirection.InAndOutRef:
					return "ref";
				default:
					return "in";
			}
		}

		private static string FormatReturnType(OperationDescription operation)
		{
			if (operation.IsOneWay)
			{
				return VoidTypeName;
			}

			// OperationDescription already unwraps Task<T>. Task, ValueTask and ValueTask<T> are unwrapped here as well.
			var type = operation.ReturnType;
			while (type != null)
			{
				if (type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask))
				{
					return VoidTypeName;
				}

				if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
				{
					type = type.GenericTypeArguments[0];
					continue;
				}

				return SoapUiTypeNames.Format(type);
			}

			return VoidTypeName;
		}

		private IReadOnlyList<PreparedService> Prepare(IReadOnlyList<SoapEndpointInfo> endpoints, bool includeOperationDetails)
		{
			var usedIds = new HashSet<string>(StringComparer.Ordinal);
			var result = new List<PreparedService>(endpoints.Count);

			foreach (var info in endpoints)
			{
				if (info == null)
				{
					continue;
				}

				// The id depends only on the registry path, and is computed before anything that can throw.
				var normalizedPath = NormalizePath(info.Path);
				var id = UniqueId(Slugify(normalizedPath), usedIds);

				try
				{
					result.Add(Project(info, id, normalizedPath, includeOperationDetails));
				}
				catch (Exception ex)
				{
					// Details stay server-side. The API only says that the endpoint could not be described.
					_logger.LogWarning(ex, "Could not describe the SOAP endpoint {Path}.", normalizedPath);
					result.Add(CreateFallback(id, normalizedPath));
				}
			}

			return result;
		}

		private PreparedService Project(SoapEndpointInfo info, string id, string normalizedPath, bool includeOperationDetails)
		{
			var serviceType = info.ServiceType;

			string name;
			IReadOnlyList<SoapUiOperationDto> operations;
			string? operationsError;
			string? description;
			if (serviceType == null)
			{
				name = normalizedPath;
				operations = Array.Empty<SoapUiOperationDto>();
				operationsError = NoServiceTypeError;
				description = null;
			}
			else
			{
				var withoutContractName = info.GenerateSoapActionWithoutContractName;
				var cache = withoutContractName ? _operationsWithoutContractName : _operationsWithContractName;
				var lazyResult = cache.GetOrAdd(serviceType, type => new Lazy<OperationsResult>(() => BuildOperations(type, withoutContractName), LazyThreadSafetyMode.ExecutionAndPublication));
				var result = lazyResult.Value;

				// The cached entry holds both versions. Picking here, per request, is what keeps the details behind the option.
				name = result.Name;
				operations = includeOperationDetails ? result.Operations : result.OperationsWithoutDetails;
				operationsError = result.Error;
				description = result.Description;
			}

			return new PreparedService
			{
				Id = id,
				NormalizedPath = normalizedPath,
				Name = name,
				ServiceType = serviceType == null ? string.Empty : SoapUiTypeNames.Format(serviceType),
				Serializer = info.Serializer.ToString(),
				CustomSerializerType = info.CustomSerializerType == null ? null : SoapUiTypeNames.Format(info.CustomSerializerType),
				SoapVersions = MapSoapVersions(info.MessageVersions),
				CaseInsensitivePath = info.CaseInsensitivePath,
				Operations = operations,
				OperationsError = operationsError,
				Description = description,
			};
		}

		private OperationsResult BuildOperations(Type type, bool withoutContractName)
		{
			try
			{
				var description = new ServiceDescription(type, withoutContractName);

				// ToList() takes a snapshot: ServiceDescription.Operations is re-evaluated lazily on every enumeration.
				var described = description.Operations.ToList();

				var serviceName = SoapUiTypeNames.Format(type);
				var serviceDescription = TryDescribe(() => _documentation.DescribeService(type, description.GeneralContract?.ContractType), "the service", serviceName);

				// First one wins, like the operation lookup in SoapCore. Then sorted by name.
				var names = new HashSet<string>(StringComparer.Ordinal);
				var operations = new List<SoapUiOperationDto>(described.Count);
				var operationsWithoutDetails = new List<SoapUiOperationDto>(described.Count);
				foreach (var operation in described)
				{
					if (!names.Add(operation.Name))
					{
						continue;
					}

					// Both versions are built once per service type. The option only decides which one a request gets.
					// The description of the operation is not a detail: both versions carry it.
					var operationDescription = TryDescribe(() => _documentation.DescribeOperation(operation.DispatchMethod, type), "operation " + operation.Name, serviceName);
					operations.Add(CreateOperation(operation, operationDescription, TryReadDetails(operation, type)));
					operationsWithoutDetails.Add(CreateOperation(operation, operationDescription, null));
				}

				operations.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
				operationsWithoutDetails.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

				return new OperationsResult(description.ServiceName ?? type.Name, serviceDescription, operations, operationsWithoutDetails, null);
			}
			catch (Exception ex)
			{
				// The reflection failure may be caused by the service type, so only the type name is logged next to the exception.
				_logger.LogWarning(ex, "Could not read the operations of {ServiceType}.", SoapUiTypeNames.Format(type));
				return new OperationsResult(type.Name, null, Array.Empty<SoapUiOperationDto>(), Array.Empty<SoapUiOperationDto>(), OperationsFailedError);
			}
		}

		private OperationDetails? TryReadDetails(OperationDescription operation, Type serviceType)
		{
			try
			{
				var serviceName = SoapUiTypeNames.Format(serviceType);
				return ReadDetails(operation, parameter => TryDescribe(() => _documentation.DescribeParameter(parameter, serviceType), "parameter " + parameter.Name + " of operation " + operation.Name, serviceName));
			}
			catch (Exception ex)
			{
				// The operation stays in the list, only without details. The outcome is cached, so this is logged once per operation.
				_logger.LogWarning(ex, "Could not read the details of operation {Operation} of {ServiceType}.", operation.Name, SoapUiTypeNames.Format(serviceType));
				return null;
			}
		}

		// A description is a nicety: when looking it up fails the description stays null, and nothing else about the service changes.
		// The outcome is cached with the operations, so this is logged once per description.
		private string? TryDescribe(Func<string?> describe, string subject, string serviceName)
		{
			try
			{
				return describe();
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not read the description of {Subject} of {ServiceType}.", subject, serviceName);
				return null;
			}
		}

		/// <summary>
		/// The PathBase independent projection of one registry entry.
		/// </summary>
		private sealed class PreparedService
		{
			public string Id { get; init; } = string.Empty;

			public string NormalizedPath { get; init; } = "/";

			public string Name { get; init; } = string.Empty;

			public string ServiceType { get; init; } = string.Empty;

			public string Serializer { get; init; } = string.Empty;

			public string? CustomSerializerType { get; init; }

			public IReadOnlyList<string> SoapVersions { get; init; } = Array.Empty<string>();

			public bool CaseInsensitivePath { get; init; }

			public IReadOnlyList<SoapUiOperationDto> Operations { get; init; } = Array.Empty<SoapUiOperationDto>();

			public string? OperationsError { get; init; }

			public string? Description { get; init; }
		}

		/// <summary>
		/// The cached outcome of describing one service type.
		/// </summary>
		private sealed class OperationsResult
		{
			public OperationsResult(string name, string? description, IReadOnlyList<SoapUiOperationDto> operations, IReadOnlyList<SoapUiOperationDto> operationsWithoutDetails, string? error)
			{
				Name = name;
				Description = description;
				Operations = operations;
				OperationsWithoutDetails = operationsWithoutDetails;
				Error = error;
			}

			public string Name { get; }

			public string? Description { get; }

			/// <summary>
			/// Gets the operations with their details. Only used when <see cref="SoapUiOptions.ExposeOperationDetails"/> is on.
			/// </summary>
			public IReadOnlyList<SoapUiOperationDto> Operations { get; }

			/// <summary>
			/// Gets the same operations with every detail left <c>null</c>. This is what a request gets by default.
			/// </summary>
			public IReadOnlyList<SoapUiOperationDto> OperationsWithoutDetails { get; }

			public string? Error { get; }
		}

		/// <summary>
		/// The parameters, return type, faults and message contract flags of one operation.
		/// </summary>
		private sealed class OperationDetails
		{
			public OperationDetails(IReadOnlyList<SoapUiParameterDto> parameters, SoapUiReturnDto returns, IReadOnlyList<string> faults, SoapUiMessageContractDto messageContract)
			{
				Parameters = parameters;
				Returns = returns;
				Faults = faults;
				MessageContract = messageContract;
			}

			public IReadOnlyList<SoapUiParameterDto> Parameters { get; }

			public SoapUiReturnDto Returns { get; }

			public IReadOnlyList<string> Faults { get; }

			public SoapUiMessageContractDto MessageContract { get; }
		}
	}
}
