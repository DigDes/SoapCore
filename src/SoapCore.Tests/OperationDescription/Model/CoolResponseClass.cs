using System.ServiceModel;

namespace SoapCore.Tests.OperationDescription.Model
{
	[MessageContract]
	public class CoolResponseClass
	{
		public string SomeString { get; set; }
	}
}
