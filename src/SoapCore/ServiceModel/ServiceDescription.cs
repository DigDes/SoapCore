using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;

namespace SoapCore.ServiceModel
{
	public class ServiceDescription
	{
		public ServiceDescription(Type serviceType)
		{
			ServiceType = serviceType;
			ServiceKnownTypes = serviceType.GetCustomAttributes<ServiceKnownTypeAttribute>(inherit: false);

			var types = Enumerable.Empty<Type>().Concat(ServiceType.GetInterfaces());
			types = types.Concat(new[] { ServiceType });

			var contracts = new List<ContractDescription>();
			foreach (var contractType in types)
			{
				foreach (var serviceContract in contractType.GetTypeInfo().GetCustomAttributes<ServiceContractAttribute>())
				{
					var contractDescription = new ContractDescription(this, contractType, serviceContract);

					contracts.Add(contractDescription);

					if (GeneralContract is null)
					{
						GeneralContract = contractDescription;
					}
					else
					{
						if (GeneralContract.GetType().IsAssignableFrom(contractDescription.GetType()))
						{
							GeneralContract = contractDescription;
						}
					}
				}
			}

			Contracts = contracts;

			ServiceName = GeneralContract?.Name ?? serviceType.Name;
		}

		public Type ServiceType { get; }
		public string ServiceName { get; }
		public ContractDescription GeneralContract { get; }
		public IEnumerable<ServiceKnownTypeAttribute> ServiceKnownTypes { get; }
		public IEnumerable<ContractDescription> Contracts { get; }
		public IEnumerable<OperationDescription> Operations => Contracts.SelectMany(c => c.Operations);
	}
}
