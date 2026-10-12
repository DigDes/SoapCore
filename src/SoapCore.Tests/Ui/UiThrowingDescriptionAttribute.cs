using System;
using System.ComponentModel;

namespace SoapCore.Tests.Ui
{
	// A description whose text cannot be read: looking it up throws.
	[AttributeUsage(AttributeTargets.All)]
	public sealed class UiThrowingDescriptionAttribute : DescriptionAttribute
	{
		public override string Description => throw new InvalidOperationException("The description cannot be read.");
	}
}
