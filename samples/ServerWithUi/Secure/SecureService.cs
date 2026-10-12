namespace ServerWithUi.Secure
{
	/// <summary>
	/// The implementation of <see cref="ISecureService"/>. It only runs for callers that <see cref="SecureDemoMessageProcessor"/> accepted.
	/// </summary>
	public sealed class SecureService : ISecureService
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		public SecureService(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public string WhoAmI()
		{
			// The processor records the scheme it accepted. A request that did not go through it has none.
			object scheme = null;
			_httpContextAccessor.HttpContext?.Items.TryGetValue(SecureDemoMessageProcessor.SchemeItemKey, out scheme);

			return scheme is string name
				? "You are the demo user, authenticated with the " + name + " scheme."
				: "You are not authenticated.";
		}

		public string Echo(string text)
		{
			return "Secure echo: " + text;
		}

		public int Add(int left, int right)
		{
			return left + right;
		}
	}
}
