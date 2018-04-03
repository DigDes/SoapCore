using System.Reflection;
using System.ServiceModel;
using System.Xml;
using System.Xml.Serialization;
using System.Linq;

namespace SoapCore
{
	public class SoapMethodParameterInfo
	{
		public ParameterInfo Parameter { get; private set; }
		public string Name { get; private set; }
		public string Namespace { get; private set; }
		public SoapMethodParameterInfo(ParameterInfo parameter, string name, string ns)
		{
			Parameter = parameter;
			Name = name;
			Namespace = ns;
		}
	}
	public class OperationDescription
	{
		public ContractDescription Contract { get; private set; }
		public string SoapAction { get; private set; }
		public string ReplyAction { get; private set; }
		public string Name { get; private set; }
		public MethodInfo DispatchMethod { get; private set; }
		public bool IsOneWay { get; private set; }
		public SoapMethodParameterInfo[] NormalParameters { get; private set; }
		public SoapMethodParameterInfo[] OutParameters { get;private set;}
		public string ReturnName {get;private set;}

		public OperationDescription(ContractDescription contract, MethodInfo operationMethod, OperationContractAttribute contractAttribute)
		{
			Contract = contract;
			Name = contractAttribute.Name ?? operationMethod.Name;
			SoapAction = contractAttribute.Action ?? $"{contract.Namespace.TrimEnd('/')}/{contract.Name}/{Name}";
			IsOneWay = contractAttribute.IsOneWay;
			ReplyAction = contractAttribute.ReplyAction;
			DispatchMethod = operationMethod;
			NormalParameters = operationMethod.GetParameters().Where(x => !x.IsOut && !x.ParameterType.IsByRef)
				.Select(info => CreateParameterInfo(info, contract)).ToArray();
			OutParameters = operationMethod.GetParameters().Where(x => x.IsOut || x.ParameterType.IsByRef)
				.Select(info => CreateParameterInfo(info, contract)).ToArray();
			ReturnName = operationMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? Name + "Result";
		}
		static SoapMethodParameterInfo CreateParameterInfo(ParameterInfo info, ContractDescription contract)
		{
			var elementAttribute = info.GetCustomAttribute<XmlElementAttribute>();
			var parameterName = !string.IsNullOrEmpty(elementAttribute?.ElementName)
									? elementAttribute.ElementName
									: info.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? info.Name;
			var parameterNs = elementAttribute?.Namespace ?? contract.Namespace;
			return new SoapMethodParameterInfo(info, parameterName, parameterNs);
		}
	}
}
