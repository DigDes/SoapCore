using System.ComponentModel;
using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// A contract described with [Description] only (no XML documentation). The tests register the interface itself as the service type.
	[Description("A service described with attributes.")]
	[ServiceContract(Namespace = "urn:ui-described")]
	public interface IUiDescribedService
	{
		[Description("Adds two numbers.")]
		[OperationContract]
		int Add([Description("The left operand.")] int left, [Description("The right operand.")] int right);

		[OperationContract]
		string Plain(string text);

		[Description("The parsed number.")]
		[OperationContract]
		bool TryParse(string text, [Description("The number, when the text is one.")] out int value);

		// Whitespace runs and line breaks become single spaces.
		[Description("  Spaces   and\r\nline  breaks \t cleaned.  ")]
		[OperationContract]
		string Messy(string text);

		// An empty description is no description.
		[Description("")]
		[OperationContract]
		string Blank(string text);

		[Description(UiDescribedConstants.Long)]
		[OperationContract]
		string Long(string text);

		// Plain text stays plain text: nothing is stripped or interpreted.
		[Description("Some <b>bold</b> & <script>alert(1)</script> text")]
		[OperationContract]
		string Markup(string text);
	}
}
