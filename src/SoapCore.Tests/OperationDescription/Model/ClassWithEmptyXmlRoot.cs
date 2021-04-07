using System.Xml.Serialization;

namespace SoapCore.Tests.OperationDescription.Model
{
	/// <summary>
	/// Test stub to test the operation description parameter extraction with emtpy XmlRootElementName.
	/// </summary>
	[XmlRoot(ElementName = "")]
	public class ClassWithEmptyXmlRoot
	{
		/// <summary>
		/// Gets or sets SomeString.
		/// </summary>
		public string SomeString { get; set; }
	}
}
