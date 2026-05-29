namespace SoapCore.Tests.Wsdl.Services
{
	public class TypeWithShouldSerializeMember
	{
		public int IntProperty { get; set; }

		public int? OptionalIntProperty { get; set; }

		public bool ShouldSerializeOptionalIntProperty()
		{
			return OptionalIntProperty.HasValue;
		}
	}
}
