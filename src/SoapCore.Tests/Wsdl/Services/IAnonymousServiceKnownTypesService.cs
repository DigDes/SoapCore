using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceKnownType(typeof(AnonymousServiceKnownTypesService.Dog))]
	[ServiceContract]
	public interface IAnonymousServiceKnownTypesService
	{
		[ServiceKnownType(typeof(AnonymousServiceKnownTypesService.Cat))]
		[OperationContract]
		object TestFromTypedToAny(AnonymousServiceKnownTypesService.Animal value);

		[ServiceKnownType(typeof(AnonymousServiceKnownTypesService.Squirrel))]
		[OperationContract]
		AnonymousServiceKnownTypesService.Animal TestFromAnyToTyped(object value);
	}

	public class AnonymousServiceKnownTypesService : IAnonymousServiceKnownTypesService
	{
		public object TestFromTypedToAny(Animal value)
		{
			return value;
		}

		public Animal TestFromAnyToTyped(object value)
		{
			if (value is null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (value is Animal result)
			{
				return result;
			}

			throw new Exception($"Object of type `{value.GetType()}` is not supported in this context.");
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

		[DataContract(Name = "Squirrel")]
		public class Squirrel : Animal
		{
		}
	}
}
