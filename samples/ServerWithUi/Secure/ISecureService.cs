using System.ServiceModel;

namespace ServerWithUi.Secure
{
	/// <summary>
	/// A tiny contract exposed at /Secure.svc, which only answers callers that authenticate. It exists to try the "Authorize" panel of the explorer:
	/// the endpoint accepts a bearer token, HTTP Basic, an API key header or a WS-Security UsernameToken (see <see cref="SecureDemoMessageProcessor"/>).
	/// The credentials are demo values that are written in the README of the sample.
	/// </summary>
	[ServiceContract(Namespace = "http://soapcore.dev/samples/secure")]
	public interface ISecureService
	{
		/// <summary>
		/// Tells which authentication scheme the caller used: Bearer, Basic, ApiKey or WsUsernameToken.
		/// </summary>
		/// <returns>A sentence that names the scheme.</returns>
		[OperationContract]
		string WhoAmI();

		/// <summary>
		/// Returns the text it is given, so a call that went through proves that the credentials were accepted.
		/// </summary>
		/// <param name="text">Any text.</param>
		/// <returns>The text, with a prefix.</returns>
		[OperationContract]
		string Echo(string text);

		/// <summary>
		/// Adds two numbers.
		/// </summary>
		/// <param name="left">The first number.</param>
		/// <param name="right">The second number.</param>
		/// <returns>The sum.</returns>
		[OperationContract]
		int Add(int left, int right);
	}
}
