namespace SoapCore.Tests.Ui
{
	// Only exists to give the catalog a generic service type whose display name must not carry assembly information.
	public class UiGenericService<T> : IUiTestService
	{
		public string Echo(string text)
		{
			return text;
		}

		public void Fire(string text)
		{
		}

		public string Custom()
		{
			return typeof(T).Name;
		}
	}
}
