namespace SoapCore.Tests.Ui
{
	// Types used in the parameter lists of IUiXmlDocService: a nested type, and a generic type nested in a generic type.
	// Their ids in the XML documentation are SoapCore.Tests.Ui.UiXmlDocModels.Inner and
	// SoapCore.Tests.Ui.UiXmlDocModels.Outer{System.Int32}.Inner{System.String} (as a parameter type).
	public static class UiXmlDocModels
	{
		public class Inner
		{
			public string Text { get; set; }
		}

		public class Outer<T>
		{
			public class Inner<TInner>
			{
				public T First { get; set; }

				public TInner Second { get; set; }
			}
		}
	}
}
