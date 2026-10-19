namespace SoapCore.UI
{
	/// <summary>
	/// How much of a service the explorer expands when the user selects it. See <see cref="SoapUiOptions.DocExpansion"/>.
	/// </summary>
	public enum SoapUiDocExpansion
	{
		/// <summary>
		/// Every operation is listed collapsed, showing only its name and summary. This is the default.
		/// </summary>
		List = 0,

		/// <summary>
		/// Every operation card is expanded as soon as a service is selected.
		/// </summary>
		Full = 1,
	}
}
