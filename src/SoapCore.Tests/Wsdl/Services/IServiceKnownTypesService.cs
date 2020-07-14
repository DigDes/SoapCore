using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceKnownType(typeof(ServiceKnownTypesService.Dog))]
	[ServiceContract]
	public interface IServiceKnownTypesService
	{
		[ServiceKnownType(typeof(ServiceKnownTypesService.Cat))]
		[OperationContract]
		ServiceKnownTypesService.Animal Test(ServiceKnownTypesService.Animal value);
	}

	public class ServiceKnownTypesService : IServiceKnownTypesService
	{
		public Animal Test(Animal value)
		{
			return value;
		}

		[DataContract(Name = "Animal")]
		public class Animal
		{
		}

		[DataContract(Name = "Dog")]
		public class Dog : Animal
		{
		}

		[DataContract(Name = "Cat")]
		public class Cat : Animal
		{
		}
	}
}
