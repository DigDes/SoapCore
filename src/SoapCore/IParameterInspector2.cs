namespace SoapCore
{
	public interface IParameterInspector2
	{
		void AfterCall(ServiceDescription serviceDescription, string operationName, object[] outputs, object returnValue, object correlationState);

		object BeforeCall(ServiceDescription serviceDescription, string operationName, object[] inputs);
	}
}
