namespace SoapCore.Tests.Wsdl.Services;

public class ComplexTypeWithGeneric
{
	public int Id { get; set; }

	public InnerGenericTypeWithSingleArgument<InnerType> SingleGenericArgument { get; set; }

	public InnerGenericTypeWithMultipleArguments<InnerType, InnerType> MultipleGenericArguments { get; set; }

	public class InnerGenericTypeWithSingleArgument<T>
		where T : class
	{
	}

	public class InnerGenericTypeWithMultipleArguments<T, TK>
		where T : class
		where TK : class
	{
	}

	public class InnerType
	{
		public string Text { get; set; }
	}
}
