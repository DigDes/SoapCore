using Microsoft.VisualStudio.TestTools.UnitTesting;

using SoapCore.Tests.Model;
namespace SoapCore.Tests
{
	public class TestServiceWithNamespaces : ITestServiceWithNamespaces
	{
		public ComplexModelWithNamespacesOutput TestOperation(ComplexModelWithNamespacesInput input)
		{
			Assert.IsNotNull(input);
			Assert.IsNotNull(input.ComplexModelNestedInput);
			return new ComplexModelWithNamespacesOutput
			{
				Output = input.ComplexModelNestedInput.NestedStringProperty
			};
		}
	}
}
