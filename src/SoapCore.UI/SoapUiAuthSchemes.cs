using System;

namespace SoapCore.UI
{
	/// <summary>
	/// The authentication schemes the "Authorize" panel of the explorer can offer. Combine values with the bitwise OR operator.
	/// See <see cref="SoapUiOptions.AuthSchemes"/>.
	/// </summary>
	[Flags]
	public enum SoapUiAuthSchemes
	{
		/// <summary>
		/// No scheme: the "Authorize" panel is hidden.
		/// </summary>
		None = 0,

		/// <summary>
		/// A bearer token, sent as <c>Authorization: Bearer {token}</c>.
		/// </summary>
		Bearer = 1,

		/// <summary>
		/// HTTP Basic authentication: the user types a user name and a password and the explorer builds the <c>Authorization: Basic</c> header.
		/// </summary>
		Basic = 2,

		/// <summary>
		/// An API key sent in a request header. The header name is <see cref="SoapUiOptions.ApiKeyHeaderName"/>.
		/// </summary>
		ApiKey = 4,

		/// <summary>
		/// A WS-Security UsernameToken, inserted as a <c>wsse:Security</c> header into the SOAP envelope.
		/// It matches what <c>AddSoapWsSecurityFilter</c> of SoapCore validates.
		/// </summary>
		WsUsernameToken = 8,

		/// <summary>
		/// Every scheme. This is the default.
		/// </summary>
		All = Bearer | Basic | ApiKey | WsUsernameToken,
	}
}
