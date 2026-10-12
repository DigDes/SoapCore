using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SoapCore.UI
{
	/// <summary>
	/// Finds the description of a service, an operation or a parameter: the text of a <see cref="DescriptionAttribute"/> (when
	/// <see cref="SoapUiOptions.UseDescriptionAttributes"/> is on), otherwise the <c>summary</c> (or the <c>param</c>) of the XML documentation files added
	/// with <see cref="SoapUiOptions.IncludeXmlComments"/>. The contract (the interface, when there is one) is consulted before the service type.
	/// The XML files are read lazily, once per path, and never again. A file that cannot be read is logged once and then ignored.
	/// Descriptions are plain text, see <see cref="SoapUiDescriptionText"/>.
	/// The caller caches the results (the catalog keeps them with the operations of each service type), so nothing is cached per member here.
	/// Nothing is read in the constructor.
	/// </summary>
	internal sealed class SoapUiDocumentationProvider
	{
		private readonly IOptions<SoapUiOptions> _options;
		private readonly ILogger<SoapUiDocumentationProvider> _logger;

		// One entry per path, whatever the outcome (null when the file could not be read).
		private readonly ConcurrentDictionary<string, Lazy<SoapUiXmlDocumentationFile?>> _files =
			new ConcurrentDictionary<string, Lazy<SoapUiXmlDocumentationFile?>>(StringComparer.Ordinal);

		public SoapUiDocumentationProvider(IOptions<SoapUiOptions> options, ILogger<SoapUiDocumentationProvider> logger)
		{
			_options = options;
			_logger = logger;
		}

		/// <summary>
		/// Describes a service.
		/// </summary>
		/// <param name="serviceType">The type the endpoint was registered with.</param>
		/// <param name="contractType">The type that carries the <c>ServiceContract</c> attribute, when known.</param>
		/// <returns>The description, or <c>null</c>.</returns>
		public string? DescribeService(Type serviceType, Type? contractType)
		{
			var types = new List<Type>(2);
			if (contractType != null)
			{
				types.Add(contractType);
			}

			if (contractType != serviceType)
			{
				types.Add(serviceType);
			}

			var options = _options.Value;
			if (options.UseDescriptionAttributes)
			{
				foreach (var type in types)
				{
					var text = FromAttribute(type.GetCustomAttribute<DescriptionAttribute>());
					if (text != null)
					{
						return text;
					}
				}
			}

			var files = GetFiles(options);
			if (files.Count == 0)
			{
				return null;
			}

			foreach (var type in types)
			{
				var id = SoapUiXmlDocIds.ForType(type);
				var text = id == null ? null : FromSummary(files, id);
				if (text != null)
				{
					return text;
				}
			}

			return null;
		}

		/// <summary>
		/// Describes an operation.
		/// </summary>
		/// <param name="method">The method of the operation, as declared by its contract.</param>
		/// <param name="serviceType">The type the endpoint was registered with. Its implementation of the method is consulted after the contract.</param>
		/// <returns>The description, or <c>null</c>.</returns>
		public string? DescribeOperation(MethodInfo method, Type serviceType)
		{
			var methods = MethodCandidates(method, serviceType);

			var options = _options.Value;
			if (options.UseDescriptionAttributes)
			{
				foreach (var candidate in methods)
				{
					var text = FromAttribute(candidate.GetCustomAttribute<DescriptionAttribute>());
					if (text != null)
					{
						return text;
					}
				}
			}

			var files = GetFiles(options);
			if (files.Count == 0)
			{
				return null;
			}

			foreach (var candidate in methods)
			{
				var id = SoapUiXmlDocIds.ForMethod(candidate);
				var text = id == null ? null : FromSummary(files, id);
				if (text != null)
				{
					return text;
				}
			}

			return null;
		}

		/// <summary>
		/// Describes a parameter of an operation.
		/// </summary>
		/// <param name="parameter">The parameter, as declared by the contract.</param>
		/// <param name="serviceType">The type the endpoint was registered with. Its implementation of the method is consulted after the contract.</param>
		/// <returns>The description, or <c>null</c>.</returns>
		public string? DescribeParameter(ParameterInfo parameter, Type serviceType)
		{
			if (parameter.Member is not MethodInfo method)
			{
				return null;
			}

			// The same parameter of each candidate method: the one of the contract, then the one of the implementation.
			var parameters = new List<ParameterInfo>(2);
			var methods = new List<MethodInfo>(2);
			foreach (var candidate in MethodCandidates(method, serviceType))
			{
				var candidateParameters = candidate.GetParameters();
				if (parameter.Position < candidateParameters.Length)
				{
					parameters.Add(candidateParameters[parameter.Position]);
					methods.Add(candidate);
				}
			}

			var options = _options.Value;
			if (options.UseDescriptionAttributes)
			{
				foreach (var candidate in parameters)
				{
					var text = FromAttribute(candidate.GetCustomAttribute<DescriptionAttribute>());
					if (text != null)
					{
						return text;
					}
				}
			}

			var files = GetFiles(options);
			if (files.Count == 0)
			{
				return null;
			}

			for (var i = 0; i < methods.Count; i++)
			{
				var name = parameters[i].Name;
				var id = SoapUiXmlDocIds.ForMethod(methods[i]);
				if (name == null || id == null)
				{
					continue;
				}

				foreach (var file in files)
				{
					var text = file.GetParameter(id, name);
					if (text != null)
					{
						return text;
					}
				}
			}

			return null;
		}

		private static string? FromAttribute(DescriptionAttribute? attribute)
		{
			return SoapUiDescriptionText.Normalize(attribute?.Description);
		}

		private static string? FromSummary(IReadOnlyList<SoapUiXmlDocumentationFile> files, string id)
		{
			foreach (var file in files)
			{
				var text = file.GetSummary(id);
				if (text != null)
				{
					return text;
				}
			}

			return null;
		}

		// The method of the contract first, then the method of the service type that implements it (when that is another method).
		private static List<MethodInfo> MethodCandidates(MethodInfo contractMethod, Type serviceType)
		{
			var methods = new List<MethodInfo>(2) { contractMethod };

			var implementation = FindImplementation(contractMethod, serviceType);
			if (implementation != null && implementation != contractMethod)
			{
				methods.Add(implementation);
			}

			return methods;
		}

		private static MethodInfo? FindImplementation(MethodInfo contractMethod, Type serviceType)
		{
			var contractType = contractMethod.DeclaringType;
			if (contractType == null || !contractType.IsInterface || !serviceType.IsClass || !contractType.IsAssignableFrom(serviceType))
			{
				return null;
			}

			try
			{
				var map = serviceType.GetInterfaceMap(contractType);
				var index = Array.IndexOf(map.InterfaceMethods, contractMethod);
				return index >= 0 ? map.TargetMethods[index] : null;
			}
			catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is InvalidOperationException)
			{
				// Best effort: the description of the contract is enough.
				return null;
			}
		}

		private List<SoapUiXmlDocumentationFile> GetFiles(SoapUiOptions options)
		{
			var files = new List<SoapUiXmlDocumentationFile>();
			foreach (var path in options.XmlCommentsFiles)
			{
				var lazy = _files.GetOrAdd(path, key => new Lazy<SoapUiXmlDocumentationFile?>(() => TryLoad(key), LazyThreadSafetyMode.ExecutionAndPublication));
				var file = lazy.Value;
				if (file != null)
				{
					files.Add(file);
				}
			}

			return files;
		}

		private SoapUiXmlDocumentationFile? TryLoad(string path)
		{
			try
			{
				return SoapUiXmlDocumentationFile.Load(path);
			}
			catch (Exception ex)
			{
				// The path stays in the server log. It is never part of what the API serves.
				_logger.LogWarning(ex, "Could not read the XML documentation file {XmlFile}. The explorer goes on without its descriptions.", path);
				return null;
			}
		}
	}
}
