using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IInheritanceService
	{
		[OperationContract]
		InheritanceService.Animal Test();
	}

	public class InheritanceService : IInheritanceService
	{
		public Animal Test()
		{
			return new Dog
			{
				Name = "Test",
			};
		}

		[KnownType(typeof(Dog))]
		[DataContract(Name = "Animal")]
		public abstract class Animal
		{
			[DataMember]
			public string Name { get; set; }
		}

		[DataContract(Name = "Dog")]
		public class Dog : Animal
		{
		}
	}
}
