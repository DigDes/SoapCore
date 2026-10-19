using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// Types that the operation details tests put in contracts: message contracts and fault details.
	public static class UiDetailsModels
	{
		[MessageContract]
		public class Request
		{
			[MessageBodyMember]
			public string Text { get; set; }
		}

		[MessageContract]
		public class Response
		{
			[MessageBodyMember]
			public string Text { get; set; }
		}

		public class Fault
		{
			public string Reason { get; set; }
		}

		public class OtherFault
		{
			public string Reason { get; set; }
		}

		// A generic fault type: each closed type counts as a different declared fault.
		public class KeyedFault<T>
		{
			public T Key { get; set; }
		}
	}
}
