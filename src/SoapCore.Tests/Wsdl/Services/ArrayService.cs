using System.Collections.Generic;
using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
	[ServiceContract(Namespace = "http://bagov.net/")]
	public interface IArrayService
	{
		[OperationContract]
		[XmlSerializerFormat]
		EnumerableResponse GetResponse(ArrayRequest request);
	}

	public class ArrayService : IArrayService
	{
		public EnumerableResponse GetResponse(ArrayRequest request)
		{
			return new EnumerableResponse();
		}
	}

	[MessageContract]
	public class EnumerableResponse
	{
		public IEnumerable<long?> LongNullableEnumerable { get; set; }
		public IEnumerable<long> LongEnumerable { get; set; }
		public IEnumerable<IEnumerable<long>> LongEnumerableEnumerable { get; set; }
		public IEnumerable<IEnumerable<string>> StringEnumerableEnumerable { get; set; }
	}

	[MessageContract]
	public class ArrayRequest
	{
		public long?[] LongNullableArray { get; set; }
		public long[] LongArray { get; set; }
		public long[][] LongArrayArray { get; set; }
		public List<List<string>> StringListList { get; set; }
		public List<innerClass> InnerClassList { get; set; }
	}

// Class starts with lower-case letter on purpose for a test
#pragma warning disable SA1300 // Element should begin with upper-case letter
	public class innerClass
#pragma warning restore SA1300 // Element should begin with upper-case letter
	{
		public string Name { get; set; }
	}
}
