using System.Collections.Generic;
using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// A contract whose operations cover the shapes of member ids in an XML documentation file. The tests write that file by hand
	// and register the interface itself as the service type, so no implementation is needed.
	[ServiceContract(Namespace = "urn:ui-xml")]
	public interface IUiXmlDocService
	{
		[OperationContract]
		string Summary(string text, int count);

		[OperationContract]
		string NoParameters();

		[OperationContract]
		string Markup(string value);

		[OperationContract]
		List<string> Generic(List<int> values, Dictionary<string, List<int>> map);

		[OperationContract]
		string Arrays(int[] values, string[][] jagged, int[,] grid);

		[OperationContract]
		bool TryRead(string text, out int value, ref string mode);

		[OperationContract]
		string Nested(UiXmlDocModels.Inner inner, UiXmlDocModels.Outer<int>.Inner<string> generic);

		[OperationContract]
		int? Optional(int? value);

		[OperationContract]
		string Whitespace(string text);

		[OperationContract]
		string Boundary(string text);

		[OperationContract]
		string Escapes(string text);

		[OperationContract]
		string Inherited(string text);
	}
}
