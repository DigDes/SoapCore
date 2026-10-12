using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SoapCore.UI
{
	/// <summary>
	/// The fixed whitelist of embedded UI files. Routes are literal strings, so nothing is ever looked up from request input.
	/// </summary>
	internal static class SoapUiAssetStore
	{
		private static readonly Lazy<IReadOnlyDictionary<string, SoapUiAsset>> Assets =
			new Lazy<IReadOnlyDictionary<string, SoapUiAsset>>(LoadAll, LazyThreadSafetyMode.ExecutionAndPublication);

		/// <summary>
		/// Returns the assets keyed by their route inside the explorer group.
		/// Throws <see cref="InvalidOperationException"/> when an embedded resource is missing.
		/// </summary>
		/// <returns>The immutable asset table.</returns>
		public static IReadOnlyDictionary<string, SoapUiAsset> Load()
		{
			return Assets.Value;
		}

		private static IReadOnlyDictionary<string, SoapUiAsset> LoadAll()
		{
			var assets = new Dictionary<string, SoapUiAsset>(StringComparer.Ordinal);
			Add(assets, "/", "SoapCore.UI.EmbeddedUI.index.html", "text/html; charset=utf-8");
			Add(assets, "/app.js", "SoapCore.UI.EmbeddedUI.app.js", "text/javascript; charset=utf-8");
			Add(assets, "/wsdl-parser.js", "SoapCore.UI.EmbeddedUI.wsdl-parser.js", "text/javascript; charset=utf-8");
			Add(assets, "/form-editor.js", "SoapCore.UI.EmbeddedUI.form-editor.js", "text/javascript; charset=utf-8");
			Add(assets, "/xml-view.js", "SoapCore.UI.EmbeddedUI.xml-view.js", "text/javascript; charset=utf-8");
			Add(assets, "/style.css", "SoapCore.UI.EmbeddedUI.style.css", "text/css; charset=utf-8");
			Add(assets, "/form-editor.css", "SoapCore.UI.EmbeddedUI.form-editor.css", "text/css; charset=utf-8");
			return assets;
		}

		private static void Add(IDictionary<string, SoapUiAsset> assets, string route, string resourceName, string contentType)
		{
			using (var stream = typeof(SoapUiAssetStore).Assembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					throw new InvalidOperationException("The embedded resource '" + resourceName + "' of SoapCore.UI is missing.");
				}

				using (var buffer = new MemoryStream())
				{
					stream.CopyTo(buffer);
					assets.Add(route, new SoapUiAsset(contentType, buffer.ToArray()));
				}
			}
		}
	}
}
