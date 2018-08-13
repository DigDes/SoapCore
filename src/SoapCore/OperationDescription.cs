using System.Reflection;
using System.ServiceModel;
using System.Xml.Serialization;
using System.Linq;

namespace SoapCore
{
	public enum SoapMethodParameterDirection
	{
		InOnly,
		OutOnlyRef,
		InAndOutRef
	}

	public class SoapMethodParameterInfo
	{
		public ParameterInfo Parameter { get; private set; }
		public int Index { get; private set; }
		public SoapMethodParameterDirection Direction { get; private set; }
		public string Name { get; private set; }
		public string Namespace { get; private set; }
		public SoapMethodParameterInfo(ParameterInfo parameter, int index, string name, string ns)
		{
			Parameter = parameter;
			Index = index;
			Name = name;
			Namespace = ns;
			if (!Parameter.IsOut && !Parameter.ParameterType.IsByRef)
			{
				Direction = SoapMethodParameterDirection.InOnly;
			}
			else if (Parameter.IsOut && Parameter.ParameterType.IsByRef)
			{
				Direction = SoapMethodParameterDirection.OutOnlyRef;
			}
			else if (!Parameter.IsOut && Parameter.ParameterType.IsByRef)
			{
				Direction = SoapMethodParameterDirection.InAndOutRef;
			}
			else
			{
				// non-ref out param (return type) not expected
				throw new System.NotImplementedException($"unexpected combination of IsOut and IsByRef in parameter {Parameter.Name} of type {Parameter.ParameterType.FullName}");
			}
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
		public bool IsMessageContractResponse { get; private set; }
		public bool IsMessageContractRequest { get; private set; }
		public SoapMethodParameterInfo[] AllParameters { get; private set; }
		public SoapMethodParameterInfo[] InParameters { get; private set; }
		public SoapMethodParameterInfo[] OutParameters { get; private set;}
		public string ReturnName {get;private set;}

		public OperationDescription(ContractDescription contract, MethodInfo operationMethod, OperationContractAttribute contractAttribute)
		{
			Contract = contract;
			Name = contractAttribute.Name ?? operationMethod.Name;
			SoapAction = contractAttribute.Action ?? $"{contract.Namespace.TrimEnd('/')}/{contract.Name}/{Name}";
			IsOneWay = contractAttribute.IsOneWay;
			IsMessageContractResponse =
				operationMethod
					.ReturnType
					.CustomAttributes
					.FirstOrDefault(ca => ca.AttributeType == typeof(MessageContractAttribute)) != null;
			ReplyAction = contractAttribute.ReplyAction;
			DispatchMethod = operationMethod;

			AllParameters = operationMethod.GetParameters()
				.Select((info, index) => CreateParameterInfo(info, index, contract))
				.ToArray();
			InParameters = AllParameters
				.Where(soapParam => soapParam.Direction != SoapMethodParameterDirection.OutOnlyRef)
				.ToArray();
			OutParameters = AllParameters
				.Where(soapParam => soapParam.Direction != SoapMethodParameterDirection.InOnly)
				.ToArray();

			IsMessageContractRequest =
				InParameters.Length == 1
				&& InParameters.First().Parameter.ParameterType
					.CustomAttributes
					.FirstOrDefault(ca =>
						ca.AttributeType == typeof(MessageContractAttribute)) != null;

			ReturnName = operationMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? Name + "Result";
		}

		static SoapMethodParameterInfo CreateParameterInfo(ParameterInfo info, int index, ContractDescription contract)
		{
			var elementAttribute = info.GetCustomAttribute<XmlElementAttribute>();
			var parameterName =
				elementAttribute?.ElementName ??
				info.GetCustomAttribute<MessageParameterAttribute>()?.Name ??
				info.ParameterType.GetCustomAttribute<MessageContractAttribute>()?.WrapperName ??
				info.Name;
			var parameterNs = elementAttribute?.Namespace ?? contract.Namespace;
			return new SoapMethodParameterInfo(info, index, parameterName, parameterNs);
		}
	}
}
