using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IStreamService
	{
		[OperationContract]
		Stream GetStream();
	}

	public class StreamService : IStreamService
	{
		public Stream GetStream()
		{
			throw new NotImplementedException();
		}
	}
}
