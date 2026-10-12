using System.Collections.Generic;
using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// A generic contract: in the XML documentation its methods are written with the type parameter markers, not with the type arguments.
	[ServiceContract(Namespace = "urn:ui-xml-generic")]
	public interface IUiXmlDocGenericService<T>
	{
		[OperationContract]
		T Echo(T value, List<T> values);
	}
}
