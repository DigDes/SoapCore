using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore.Tests.Serialization.Models.DataContract
{
	[MessageContract(WrapperNamespace = "TestNamespace")]
	public class MessageHeadersModel
	{
		[MessageHeader]
		public string Prop1 { get; set; }
	}
}
