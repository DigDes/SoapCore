namespace SoapCore.Tests.Ui
{
	public class UiBrokenService : IUiBrokenService
	{
		public string Broken(string text)
		{
			return text;
		}
	}
}
