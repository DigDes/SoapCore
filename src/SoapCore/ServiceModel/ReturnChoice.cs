using System;

namespace SoapCore.ServiceModel
{
	public class ReturnChoice
	{
		public ReturnChoice(Type type, string name, string @namespace)
		{
			Type = type;
			Name = name;
			Namespace = @namespace;
		}

		public Type Type { get; private set; }
		public string Name { get; private set; }
		public string Namespace { get; private set; }
	}
}
