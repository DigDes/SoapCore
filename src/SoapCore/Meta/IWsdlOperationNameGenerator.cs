using SoapCore.ServiceModel;

namespace SoapCore.Meta
{
	public interface IWsdlOperationNameGenerator
	{
		string GenerateWsdlInputMessageName(OperationDescription operation, ServiceDescription service);
		string GenerateWsdlOutputMessageName(OperationDescription operation, ServiceDescription service);
	}

	public class DefaultWsdlOperationNameGenerator : IWsdlOperationNameGenerator
	{
		public string GenerateWsdlInputMessageName(OperationDescription operation, ServiceDescription service)
		{
			return $"{service.GeneralContract.Name}_{operation.Name}_InputMessage";
		}

		public string GenerateWsdlOutputMessageName(OperationDescription operation, ServiceDescription service)
		{
			return $"{service.GeneralContract.Name}_{operation.Name}_OutputMessage";
		}
	}

	public class LegacyWsdlOperationNameGenerator : IWsdlOperationNameGenerator
	{
		public string GenerateWsdlInputMessageName(OperationDescription operation, ServiceDescription service)
		{
			return $"{operation.Name}SoapIn";
		}

		public string GenerateWsdlOutputMessageName(OperationDescription operation, ServiceDescription service)
		{
			return $"{operation.Name}SoapOut";
		}
	}
}
