using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SoapCore.ServiceModel
{
	public class OperationDescription
	{
		public OperationDescription(ContractDescription contract, MethodInfo operationMethod, OperationContractAttribute contractAttribute)
		{
			Contract = contract;
			Name = contractAttribute.Name ?? GetNameByAction(contractAttribute.Action) ?? GetNameByMethod(operationMethod);
			SoapAction = contractAttribute.Action ?? $"{contract.Namespace.TrimEnd('/')}/{contract.Name}/{Name}";
			IsOneWay = contractAttribute.IsOneWay;
			DispatchMethod = operationMethod;

			var returnType = operationMethod.ReturnType;

			if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				returnType = returnType.GenericTypeArguments[0];
			}

			IsMessageContractResponse = returnType.CustomAttributes
					 .FirstOrDefault(ca => ca.AttributeType == typeof(MessageContractAttribute)) != null;

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

			var elementAttribute = operationMethod.ReturnParameter.GetCustomAttribute<XmlElementAttribute>();
			ReturnName = operationMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? Name + "Result";
			ReturnElementName = elementAttribute?.ElementName;
			ReturnNamespace = elementAttribute?.Form == XmlSchemaForm.Unqualified ? string.Empty : elementAttribute?.Namespace;

			ReplyAction = contractAttribute.ReplyAction ?? $"{Contract.Namespace.TrimEnd('/')}/{contract.Name}/{Name + "Response"}";

			var faultContractAttributes = operationMethod.GetCustomAttributes<FaultContractAttribute>();
			Faults = faultContractAttributes
				.Where(a => a.DetailType?.Name != null)
				.Select(a => a.DetailType)
				.ToArray();
		}

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
		public SoapMethodParameterInfo[] OutParameters { get; private set; }
		public System.Type[] Faults { get; private set; }
		public string ReturnName { get; private set; }
		public string ReturnElementName { get; private set; }
		public string ReturnNamespace { get; private set; }

		private static SoapMethodParameterInfo CreateParameterInfo(ParameterInfo info, int index, ContractDescription contract)
		{
			var elementAttribute = info.GetCustomAttribute<XmlElementAttribute>();
			var arrayAttribute = info.GetCustomAttribute<XmlArrayAttribute>();
			var arrayItemAttribute = info.GetCustomAttribute<XmlArrayItemAttribute>();
			var parameterName = elementAttribute?.ElementName
				?? arrayAttribute?.ElementName
				?? info.GetCustomAttribute<MessageParameterAttribute>()?.Name
				?? info.ParameterType.GetCustomAttribute<MessageContractAttribute>()?.WrapperName
				?? info.Name;
			var arrayName = arrayAttribute?.ElementName;
			var arrayItemName = arrayItemAttribute?.ElementName;
			var parameterNs = elementAttribute?.Form == XmlSchemaForm.Unqualified
				? string.Empty
				: elementAttribute?.Namespace
				?? arrayAttribute?.Namespace
				?? contract.Namespace;
			var dataContractAttribute = info.ParameterType.GetCustomAttribute<DataContractAttribute>();
			if (dataContractAttribute != null && dataContractAttribute.IsNamespaceSetExplicitly && !string.IsNullOrWhiteSpace(dataContractAttribute.Namespace))
			{
				parameterNs = dataContractAttribute.Namespace;
			}

			return new SoapMethodParameterInfo(info, index, parameterName, arrayName, arrayItemName, parameterNs);
		}

		private static string GetNameByAction(string action)
		{
			var index = action?.LastIndexOf("/");
			return (index ?? -1) > -1
				? action.Substring(index.Value + 1, action.Length - index.Value - 1)
				: null;
		}

		private static string GetNameByMethod(MethodInfo operationMethod)
		{
			var returnType = operationMethod.ReturnType;
			var name = operationMethod.Name;

			if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				if (name.EndsWith("Async"))
				{
					name = name.Substring(0, name.LastIndexOf("Async"));
				}
			}

			if (returnType == typeof(Task) && name.EndsWith("Async"))
			{
				name = name.Substring(0, name.LastIndexOf("Async"));
			}

			return name;
		}
	}
}
