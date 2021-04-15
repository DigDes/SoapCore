using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	//same as the DataContractWithStream, simply added namespace in MessageHeader for testing
	[MessageContract(WrapperNamespace = "TestNamespace")]
	public class DataContractWithStream2
	{
		[MessageHeader(Namespace = "TestHeaderNamespace")]
		public string Header { get; set; }

		[MessageBodyMember]
		public Stream Data { get; set; }
	}
}
