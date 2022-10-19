using System.ComponentModel;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
	[ServiceContract(Namespace = "http://bagov.net")]
	public interface IDefaultValueAttributesService
	{
		[OperationContract]
		DefaultValueAttributesResponseType GetResponse();
	}

	public class DefaultValueAttributesService : IDefaultValueAttributesService
	{
		public DefaultValueAttributesResponseType GetResponse()
		{
			return new DefaultValueAttributesResponseType();
		}
	}

	public class DefaultValueAttributesResponseType
	{
		public bool BooleanWithNoDefaultProperty { get; set; }

		[DefaultValue(null)]
		public bool BooleanWithDefaultNullProperty { get; set; }

		[DefaultValue(false)]
		public bool BooleanWithDefaultFalseProperty { get; set; }

		[DefaultValue(true)]
		public string BooleanWithDefaultTrueProperty { get; set; }

		public int IntWithNoDefaultProperty { get; set; }

		[DefaultValue(42)]
		public int IntWithDefaultProperty { get; set; }

		public string StringWithNoDefaultProperty { get; set; }

		[DefaultValue(null)]
		public string StringWithDefaultNullProperty { get; set; }

		[DefaultValue("default")]
		public string StringWithDefaultProperty { get; set; }
	}
}
