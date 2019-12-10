using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IStructsService
	{
		[OperationContract]
		void Method(StructService.StructModel model);
	}

	public class StructService : IStructsService
	{
		public void Method(StructModel model)
		{
			throw new NotImplementedException();
		}

		[DataContract]
		public struct AnyStruct
		{
			public int Prop { get; set; }
		}

		[DataContract]
		public class StructModel
		{
			[DataMember]
			public IList<AnyStruct> MyStructs { get; set; }
		}
	}
}
