namespace SoapCore.Tests.Ui
{
	public class UiTestService : IUiTestService
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
			return "custom";
		}
	}
}
