using System;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	internal interface IXmlIncludeService
	{
		[XmlInclude(typeof(XmlIncludeService.Dog))]
		[XmlInclude(typeof(XmlIncludeService.Cat))]
		[OperationContract]
		XmlIncludeService.Animal Test(XmlIncludeService.Animal value);
	}

	public class XmlIncludeService : IXmlIncludeService
	{
		public Animal Test(Animal value)
		{
			return value;
		}

		[Serializable]
		[XmlInclude(typeof(Dog))]
		[XmlInclude(typeof(Cat))]
		public class Animal
		{
		}

		[Serializable]
		public class Dog : Animal
		{
		}

		[Serializable]
		public class Cat : Animal
		{
		}
	}
}
