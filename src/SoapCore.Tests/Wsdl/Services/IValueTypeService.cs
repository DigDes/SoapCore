using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IValueTypeService
	{
		[OperationContract]
		void MethodInput(ValueTypeService.AnyStructInput model);

		[OperationContract]
		ValueTypeService.AnyStructOutput MethodOutput();
	}

	public class ValueTypeService : IValueTypeService
	{
		public void MethodInput(AnyStructInput model)
		{
			// TODO:
		}

		public AnyStructOutput MethodOutput()
		{
			return default;
		}

		[DataContract]
		public struct AnyStructInput
		{
			[DataMember]
			public int Value { get; set; }

			[DataMember]
			public OtherStructInput? NullableOther { get; set; }
		}

		[DataContract]
		public struct OtherStructInput
		{
			[DataMember]
			public DateTime? Test { get; set; }
		}

		[DataContract]
		public struct AnyStructOutput
		{
			[DataMember]
			public double Value { get; set; }
		}
	}
}
