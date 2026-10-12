using System;
using System.Collections.Generic;
using System.IO;

namespace SoapCore.UI
{
	/// <summary>
	/// Options of the SoapCore.UI explorer. Bound with the Options pattern; validated when <c>MapSoapCoreUi</c> runs.
	/// </summary>
	public sealed class SoapUiOptions
	{
		private readonly List<string> _xmlCommentsFiles = new List<string>();

		/// <summary>
		/// Gets or sets the title shown in the page header and browser tab. Plain text, never interpreted as HTML.
		/// Must be 1 to 100 characters, not whitespace only, with no control characters.
		/// Defaults to 'SoapCore Explorer'.
		/// </summary>
		public string Title { get; set; } = "SoapCore Explorer";

		/// <summary>
		/// Gets or sets a value indicating whether the UI lets the user send requests. Defaults to <c>true</c>.
		/// When <c>false</c> the UI hides and disables sending.
		/// This is a UI convenience only, not a security boundary: the SOAP endpoints stay reachable.
		/// Use authentication and authorization to restrict access.
		/// </summary>
		public bool EnableTryItOut { get; set; } = true;

		/// <summary>
		/// Gets or sets a path prefix inserted between the request PathBase and each registered endpoint path when the UI builds endpoint URLs.
		/// Use it when the SOAP endpoints sit behind <c>app.Map(...)</c>, or behind a reverse proxy that strips a path prefix,
		/// because the registry does not record such prefixes. Empty by default.
		/// The same prefix applies to ALL endpoints, so it must match how all of them are hosted.
		/// <para>
		/// Must be empty, or start with '/' and consist of one or more segments of letters, digits, '.', '_', '~' or '-'.
		/// No trailing '/' and no '.' or '..' segments. Max 200 characters.
		/// </para>
		/// </summary>
		public string EndpointPathPrefix { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets a value indicating whether the explorer API also describes each operation: the name, CLR type and direction
		/// (in, out or ref) of its parameters, its return type, its declared faults and whether it uses message contracts. Defaults to <c>false</c>.
		/// <para>
		/// This is opt-in because it reveals the CLR type names (namespace and class name, never the assembly) of the parameters, return values and
		/// faults of every operation to anyone who can open the explorer. The WSDL already shows the XML shape of the contract, but not the .NET types behind it.
		/// Enable it when the explorer is as private as the code of the service, for example in Development or behind <c>RequireAuthorization()</c>.
		/// </para>
		/// <para>
		/// When <c>false</c> the <c>parameters</c>, <c>returns</c>, <c>faults</c> and <c>messageContract</c> members of every operation are <c>null</c>
		/// and the catalog reports <c>operationDetailsEnabled</c> as <c>false</c>. The value is read on every request.
		/// The descriptions of the parameters are part of the details, so they follow this option too.
		/// </para>
		/// </summary>
		public bool ExposeOperationDetails { get; set; }

		/// <summary>
		/// Gets or sets an optional subtitle shown under the title. Plain text, never interpreted as HTML (the explorer writes it with <c>textContent</c>).
		/// When set it must be at most 300 characters and contain no control characters (so no line breaks).
		/// <c>null</c>, empty or whitespace only means no subtitle. Defaults to <c>null</c>.
		/// </summary>
		public string? Description { get; set; }

		/// <summary>
		/// Gets or sets the SOAP version the explorer preselects, <c>"1.1"</c> or <c>"1.2"</c>.
		/// <c>null</c> (or empty) means automatic: the explorer picks the first version the endpoint exposes.
		/// The preselection only applies to endpoints that expose that version; any other endpoint keeps the automatic choice.
		/// The user can always change it. Defaults to <c>null</c>.
		/// </summary>
		public string? DefaultSoapVersion { get; set; }

		/// <summary>
		/// Gets or sets how much of a service the explorer expands when the user selects it.
		/// <see cref="SoapUiDocExpansion.List"/> (the default) lists every operation collapsed.
		/// <see cref="SoapUiDocExpansion.Full"/> expands every operation card.
		/// The user can still expand and collapse operations, individually or all at once.
		/// </summary>
		public SoapUiDocExpansion DocExpansion { get; set; } = SoapUiDocExpansion.List;

		/// <summary>
		/// Gets or sets the authentication schemes the "Authorize" panel offers. Defaults to <see cref="SoapUiAuthSchemes.All"/>.
		/// <see cref="SoapUiAuthSchemes.None"/> hides the panel.
		/// <para>
		/// The panel is a convenience for trying services out. It is not an authentication boundary: it only adds headers
		/// (or a WS-Security header) to the requests of the explorer, in memory, and the server must still check them.
		/// </para>
		/// </summary>
		public SoapUiAuthSchemes AuthSchemes { get; set; } = SoapUiAuthSchemes.All;

		/// <summary>
		/// Gets or sets the name of the request header the "API key" scheme of the "Authorize" panel uses. Defaults to 'X-Api-Key'.
		/// Must be 1 to 128 characters of letters, digits, '-' or '_'. Names that browsers do not let a page set
		/// (such as Host, Cookie, Origin, Referer, Content-Length, and the Proxy-*, Sec-* and Access-Control-Request-* families)
		/// and the headers the explorer sets itself (Content-Type and SOAPAction) are rejected.
		/// This is a name, never a secret: it is served to every user of the explorer.
		/// </summary>
		public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

		/// <summary>
		/// Gets or sets an optional extra style sheet of the same origin, loaded after the built-in one, so the page can follow your branding.
		/// <c>null</c> or empty means none. Defaults to <c>null</c>.
		/// <para>
		/// It must be a path that starts with a single '/', made of letters, digits, '.', '_', '~', '-', '%' and '/', optionally followed by a query
		/// ('?' and then the same characters plus '=' and '&amp;'). At most 200 characters. Absolute URLs ('https://...'), scheme-relative URLs ('//host/...'),
		/// backslashes, spaces and fragments are rejected, so the style sheet always comes from the origin of the explorer
		/// (the built-in Content-Security-Policy allows 'self' styles only). The value is served to the browser exactly as written:
		/// it is not combined with the request PathBase. Serve the file yourself, for example with <c>UseStaticFiles</c>.
		/// </para>
		/// </summary>
		public string? StylesheetUrl { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="System.ComponentModel.DescriptionAttribute"/> is read to describe services,
		/// operations and parameters. Applies to the service type, its contract interfaces, the operation methods and their parameters.
		/// Defaults to <c>true</c>. The XML documentation files added with <see cref="IncludeXmlComments"/> are used whatever this value is.
		/// A description is the attribute text when there is one, otherwise the <c>summary</c> of the XML documentation.
		/// Read when a service type is first described, then cached with its operations.
		/// </summary>
		public bool UseDescriptionAttributes { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the explorer offers the Form view of the request, next to the XML view. Defaults to <c>true</c>.
		/// The Form view shows the elements of the request as typed fields (numbers, dates, choices for enumerations, add and remove for lists)
		/// and always rebuilds from the XML, which stays the source of truth. When <c>false</c> the explorer hides it and only the XML view is available.
		/// This is a UI convenience only: it changes what the page shows, nothing on the server.
		/// </summary>
		public bool EnableFormEditor { get; set; } = true;

		/// <summary>
		/// Gets or sets the timeout, in seconds, of one request that the explorer sends from the browser. Defaults to 60. Must be from 1 to 600.
		/// It is both the default of the timeout field of the UI and the largest value the user can enter there (the user can lower it).
		/// <para>
		/// It is a limit of the page, not a protection of the server: the browser stops waiting and cancels the request, but the SOAP endpoint
		/// may keep working on it. Use the limits of the web server and of the SOAP service itself to protect them.
		/// </para>
		/// </summary>
		public int SendTimeoutSeconds { get; set; } = SoapUiLimits.DefaultSendTimeoutSeconds;

		/// <summary>
		/// Gets or sets the largest response body, in bytes, that the explorer reads and shows. Defaults to 5242880 (5 MiB). Must be from 1024 to 52428800 (50 MiB).
		/// A larger response is cut at this size and the UI says so.
		/// <para>
		/// It is a limit of the page, not a protection of the server: the browser stops reading, but the SOAP endpoint has already produced the response.
		/// Use the limits of the web server and of the SOAP service itself to protect them.
		/// </para>
		/// </summary>
		public int MaxResponseBytes { get; set; } = SoapUiLimits.DefaultMaxResponseBytes;

		/// <summary>
		/// Gets the XML documentation files added with <see cref="IncludeXmlComments"/>, in the order they were added.
		/// </summary>
		public IReadOnlyList<string> XmlCommentsFiles => _xmlCommentsFiles;

		/// <summary>
		/// Adds an XML documentation file (the file the compiler writes when <c>GenerateDocumentationFile</c> is on) whose <c>summary</c>
		/// and <c>param</c> texts describe the services, operations and parameters, like the XML comments of Swashbuckle.
		/// </summary>
		/// <param name="xmlFilePath">
		/// The path of the XML file. A relative path is resolved against the current directory of the process, so prefer an absolute one,
		/// for example <c>Path.Combine(AppContext.BaseDirectory, "MyService.xml")</c>.
		/// </param>
		/// <returns>The same options, for chaining.</returns>
		/// <remarks>
		/// The file is read lazily, once, the first time the explorer describes a service type. A file that is missing, unreadable or not valid XML
		/// is ignored: one warning is logged and the explorer keeps working without it (files that declare a DTD are refused too).
		/// The same path added twice counts once. Only the text of the <c>summary</c> and <c>param</c> elements is used, as plain text.
		/// The path itself is never sent to the browser.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="xmlFilePath"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException"><paramref name="xmlFilePath"/> is empty, whitespace only, or contains characters that a path cannot have.</exception>
		public SoapUiOptions IncludeXmlComments(string xmlFilePath)
		{
			if (xmlFilePath is null)
			{
				throw new ArgumentNullException(nameof(xmlFilePath));
			}

			if (string.IsNullOrWhiteSpace(xmlFilePath))
			{
				throw new ArgumentException("The path of the XML documentation file must not be empty.", nameof(xmlFilePath));
			}

			if (xmlFilePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
			{
				throw new ArgumentException("The path of the XML documentation file contains characters that a path cannot have.", nameof(xmlFilePath));
			}

			if (!_xmlCommentsFiles.Contains(xmlFilePath))
			{
				_xmlCommentsFiles.Add(xmlFilePath);
			}

			return this;
		}
	}
}
