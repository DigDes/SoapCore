using System.ComponentModel;

namespace SoapCore.Tests.Ui
{
	// What the contract does not say, the implementation does. The contract has the last word when both say something.
	[Description("From the implementation type.")]
	public class UiImplementationDescribedService : IUiImplementationDescribedService
	{
		[Description("From the implementation.")]
		public string Work([Description("Implementation parameter.")] string input)
		{
			return input;
		}

		[Description("From the implementation, so it loses.")]
		public string Both([Description("Implementation parameter, so it loses.")] string input)
		{
			return input;
		}
	}
}
