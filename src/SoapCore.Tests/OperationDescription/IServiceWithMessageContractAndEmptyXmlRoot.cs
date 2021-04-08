using SoapCore.Tests.OperationDescription.Model;

namespace SoapCore.Tests.OperationDescription
{
	/// <summary>
	/// Interface to test OperationDescription.cs functionality.
	/// </summary>
	public interface IServiceWithMessageContractAndEmptyXmlRoot : IServiceWithMessageContract
	{
		/// <summary>
		/// Returns a class with an empty string as XMLRoot element.
		/// </summary>
		/// <param name="classWithXmlRoot">Class with empty string as XMLRoot element.</param>
		/// <returns><see cref="ClassWithEmptyXmlRoot"/> instance used in a test.</returns>
		ClassWithEmptyXmlRoot GetClassWithEmptyXmlRoot(ClassWithEmptyXmlRoot classWithXmlRoot);
	}
}
