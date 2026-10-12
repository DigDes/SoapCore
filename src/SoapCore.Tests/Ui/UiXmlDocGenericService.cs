using System.Collections.Generic;

namespace SoapCore.Tests.Ui
{
	// Implements a constructed generic contract, so the contract methods the explorer sees belong to a constructed generic type.
	public class UiXmlDocGenericService : IUiXmlDocGenericService<string>
	{
		public string Echo(string value, List<string> values)
		{
			return value;
		}
	}
}
