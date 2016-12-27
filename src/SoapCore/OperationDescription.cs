using System.Reflection;
using System.ServiceModel;

namespace SoapCore
{
	public class OperationDescription
	{
		public ContractDescription Contract { get; private set; }
		public string SoapAction { get; private set; }
		public string ReplyAction { get; private set; }
		public string Name { get; private set; }
		public MethodInfo DispatchMethod { get; private set; }
		public bool IsOneWay { get; private set; }

		public OperationDescription(ContractDescription contract, MethodInfo operationMethod, OperationContractAttribute contractAttribute)
		{
			Contract = contract;
			Name = contractAttribute.Name ?? operationMethod.Name;
			SoapAction = contractAttribute.Action ?? $"{contract.Namespace.TrimEnd('/')}/{contract.Name}/{Name}";
			IsOneWay = contractAttribute.IsOneWay;
			ReplyAction = contractAttribute.ReplyAction;
			DispatchMethod = operationMethod;
		}
	}
}
