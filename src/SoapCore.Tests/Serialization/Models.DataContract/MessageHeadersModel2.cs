using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	//same as MessageHeadersModel, simply added namespace in MessageHeaderAttribute for testing
	[MessageContract(WrapperNamespace = "TestNamespace")]
	public class MessageHeadersModel2
	{
		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Prop1 { get; set; }
	}
}
