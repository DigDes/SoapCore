using System;

namespace SoapCore.Tests.ParameterInspector
{
	public class ParameterInspector2Mock : IParameterInspector2
	{
		public void AfterCall(ServiceDescription serviceDescription, string operationName, object[] outputs, object returnValue, object correlationState)
		{
			if (correlationState != serviceDescription)
			{
				throw new Exception();
			}

			if (outputs.Length > 0)
			{
				outputs[0] = nameof(AfterCall);
			}
		}

		public object BeforeCall(ServiceDescription serviceDescription, string operationName, object[] inputs)
		{
			inputs[0] = nameof(BeforeCall);

			return serviceDescription;
		}
	}
}
