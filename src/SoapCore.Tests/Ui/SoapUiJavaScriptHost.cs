using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Jint;
using SoapCore.UI;

namespace SoapCore.Tests.Ui
{
	// Runs the page independent logic of the explorer (wsdl-parser.js) in the Jint interpreter, so CI can test it without a browser.
	// The script is read from the compiled assembly resource, which is exactly what the server serves. Every call gets a fresh
	// engine, so tests cannot leak state into each other, and every engine has limits, so a regression cannot hang the build.
	// Only the functions that need no DOM can run here (parse, buildEnvelope, prettyPrintXml and friends need DOMParser).
	public static class SoapUiJavaScriptHost
	{
		private const string ScriptResource = "SoapCore.UI.EmbeddedUI.wsdl-parser.js";

		private static readonly Lazy<string> Script = new Lazy<string>(ReadScript);

		// Evaluates a JavaScript expression and returns its JSON.stringify result. window.SoapUiWsdl is available.
		public static JsonElement Evaluate(string expression)
		{
			var engine = CreateEngine();
			var result = engine.Evaluate("JSON.stringify(" + expression + ")");
			if (result.IsUndefined())
			{
				throw new InvalidOperationException("The expression has no JSON representation (undefined): " + expression);
			}

			using var document = JsonDocument.Parse(result.ToString());
			return document.RootElement.Clone();
		}

		// Calls window.SoapUiWsdl.<function>(<arguments>). The function can be a path such as "curl.build".
		// Every argument is JavaScript source text, use Literal for string values.
		public static JsonElement Call(string function, params string[] arguments)
		{
			return Evaluate("window.SoapUiWsdl." + function + "(" + string.Join(", ", arguments) + ")");
		}

		// A JavaScript string literal for any text, control characters and non ASCII characters included.
		// JSON is valid JavaScript, and the default encoder writes everything except plain ASCII as \uXXXX.
		public static string Literal(string value)
		{
			return value == null ? "null" : JsonSerializer.Serialize(value);
		}

		private static Engine CreateEngine()
		{
			var engine = new Engine(options =>
			{
				options.TimeoutInterval(TimeSpan.FromSeconds(10));
				options.LimitRecursion(200);
				options.MaxStatements(500000);
			});

			// The script is written for a browser. The functions under test only need the window object to publish themselves on.
			engine.Execute("var window = {};");
			engine.Execute(Script.Value);
			return engine;
		}

		private static string ReadScript()
		{
			var assembly = typeof(SoapUiOptions).Assembly;
			using var stream = assembly.GetManifestResourceStream(ScriptResource);
			if (stream == null)
			{
				throw new InvalidOperationException("The embedded resource " + ScriptResource + " was not found in " + assembly.GetName().Name + ".");
			}

			using var reader = new StreamReader(stream, Encoding.UTF8);
			return reader.ReadToEnd();
		}
	}
}
