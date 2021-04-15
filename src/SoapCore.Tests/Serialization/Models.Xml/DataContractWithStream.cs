using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperNamespace = "TestNamespace")]
	public class DataContractWithStream
	{
		[MessageHeader]
		public string Header { get; set; }

		[MessageBodyMember]
		public Stream Data { get; set; }
	}
}
