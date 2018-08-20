using System.Reflection;

namespace SoapCore
{
	public class SoapMethodParameterInfo
	{
		public SoapMethodParameterInfo(ParameterInfo parameter, int index, string name, string ns)
		{
			Parameter = parameter;
			Index = index;
			Name = name;
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
		public string Namespace { get; private set; }
	}
}
