using System;
using System.Collections.Generic;
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

			ReturnType = operationMethod.ReturnType;
			if (ReturnType.IsGenericType && ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				ReturnType = ReturnType.GenericTypeArguments[0];
			}

			IsMessageContractResponse = ReturnType.CustomAttributes
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

			var elementAttributes = operationMethod.ReturnParameter.GetCustomAttributes<XmlElementAttribute>().ToList();
			if (elementAttributes.Count > 1)
			{
				ReturnChoices = elementAttributes.Select(e => new ReturnChoice(e.Type, e.ElementName, e.Namespace));
			}
			else if (elementAttributes.Count == 1)
			{
				var elementAttribute = elementAttributes.First();
				ReturnElementName = elementAttribute.ElementName;
				ReturnNamespace = elementAttribute.Form == XmlSchemaForm.Unqualified ? string.Empty : elementAttribute.Namespace;
			}

			ReturnName = operationMethod.ReturnParameter.GetCustomAttribute<MessageParameterAttribute>()?.Name ?? Name + "Result";

			ReplyAction = contractAttribute.ReplyAction ?? $"{Contract.Namespace.TrimEnd('/')}/{contract.Name}/{Name + "Response"}";

			var faultContractAttributes = operationMethod.GetCustomAttributes<FaultContractAttribute>();
			Faults = faultContractAttributes
				.Where(a => a.DetailType?.Name != null)
				.Select(a => a.DetailType)
				.ToArray();

			ServiceKnownTypes = operationMethod.GetCustomAttributes<ServiceKnownTypeAttribute>(inherit: false);
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
		public Type ReturnType { get; private set; }
		public IEnumerable<ServiceKnownTypeAttribute> ServiceKnownTypes { get; private set; }
		public IEnumerable<ReturnChoice> ReturnChoices { get; private set; }
		public bool ReturnsChoice => ReturnChoices != null;

		public IEnumerable<ServiceKnownTypeAttribute> GetServiceKnownTypesHierarchy()
		{
			foreach (ServiceKnownTypeAttribute serviceKnownType in ServiceKnownTypes)
			{
				yield return serviceKnownType;
			}

			foreach (ServiceKnownTypeAttribute serviceKnownType in Contract.ServiceKnownTypes)
			{
				yield return serviceKnownType;
			}

			// TODO: should we process service implementation service known type attributes
			foreach (ServiceKnownTypeAttribute serviceKnownType in Contract.Service.ServiceKnownTypes)
			{
				yield return serviceKnownType;
			}
		}

		private static SoapMethodParameterInfo CreateParameterInfo(ParameterInfo info, int index, ContractDescription contract)
		{
			var elementAttribute = info.GetCustomAttribute<XmlElementAttribute>();
			var arrayAttribute = info.GetCustomAttribute<XmlArrayAttribute>();
			var rootAttribute = (XmlRootAttribute)Attribute.GetCustomAttribute(info.ParameterType, typeof(XmlRootAttribute));
			var arrayItemAttribute = info.GetCustomAttribute<XmlArrayItemAttribute>();

			var parameterName = string.IsNullOrEmpty(elementAttribute?.ElementName)
				? string.IsNullOrEmpty(arrayAttribute?.ElementName)
					? string.IsNullOrEmpty(rootAttribute?.ElementName)
						? string.IsNullOrEmpty(info.GetCustomAttribute<MessageParameterAttribute>()?.Name)
							? string.IsNullOrEmpty(info.ParameterType.GetCustomAttribute<MessageContractAttribute>()?.WrapperName)
								? info.Name
								: info.ParameterType.GetCustomAttribute<MessageContractAttribute>().WrapperName
							: info.GetCustomAttribute<MessageParameterAttribute>().Name
						: rootAttribute.ElementName
					: arrayAttribute.ElementName
				: elementAttribute.ElementName;

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
