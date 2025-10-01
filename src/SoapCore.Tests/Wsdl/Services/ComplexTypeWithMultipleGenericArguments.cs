namespace SoapCore.Tests.Wsdl.Services;

public class ComplexTypeWithMultipleGenericArguments
{
	public int Id { get; set; }

	public InnerGenericType<InnerType, InnerType> InnerObject { get; set; }

	public class InnerGenericType<T, TK>
		where T : class
		where TK : class
	{
	}

	public class InnerType
	{
		public string Text { get; set; }
	}
}
