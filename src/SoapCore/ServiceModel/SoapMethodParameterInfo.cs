using System.Reflection;

namespace SoapCore.ServiceModel
{
	public class SoapMethodParameterInfo
	{
		public SoapMethodParameterInfo(ParameterInfo parameter, int index, string name, string arrayName, string arrayItemName, string ns)
		{
			Parameter = parameter;
			Index = index;
			Name = name;
			ArrayName = arrayName;
			ArrayItemName = arrayItemName;
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

		public ParameterInfo Parameter { get; private set; }
		public int Index { get; private set; }
		public SoapMethodParameterDirection Direction { get; private set; }
		public string Name { get; private set; }
		public string ArrayName { get; private set; }
		public string ArrayItemName { get; private set; }
		public string Namespace { get; private set; }
	}
}
