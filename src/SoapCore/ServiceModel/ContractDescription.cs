using System;
using System.Collections.Generic;
using System.Reflection;
using System.ServiceModel;

namespace SoapCore.ServiceModel
{
	public class ContractDescription
	{
		public ContractDescription(ServiceDescription service, Type contractType, ServiceContractAttribute attribute, bool generateSoapActionWithoutContractName)
		{
			Service = service;
			ContractType = contractType;
			ServiceKnownTypes = contractType.GetCustomAttributes<ServiceKnownTypeAttribute>(inherit: false);
			Namespace = attribute.Namespace ?? "http://tempuri.org/"; // Namespace defaults to http://tempuri.org/
			Name = attribute.Name ?? ContractType.Name; // Name defaults to the type name

			var operations = new List<OperationDescription>();
			foreach (var operationMethodInfo in ContractType.GetTypeInfo().DeclaredMethods)
			{
				foreach (var operationContract in operationMethodInfo.GetCustomAttributes<OperationContractAttribute>())
				{
					operations.Add(new OperationDescription(this, operationMethodInfo, operationContract, generateSoapActionWithoutContractName));
				}
			}

			Operations = operations;
		}

		public ServiceDescription Service { get; }
		public IEnumerable<ServiceKnownTypeAttribute> ServiceKnownTypes { get; }
		public string Name { get; }
		public string Namespace { get; }
		public Type ContractType { get; }
		public IEnumerable<OperationDescription> Operations { get; }
	}
}
