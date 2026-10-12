using System.Xml.Linq;
using Models;

namespace ServerWithUi
{
	/// <summary>
	/// Implementation of the shared <see cref="ISampleService"/> contract. It is exposed twice, at
	/// /Service.svc (DataContractSerializer) and at /Service.asmx (XmlSerializer), so the explorer
	/// can show how the two serializers describe the same contract.
	/// </summary>
	public class SampleService : ISampleService
	{
		public string Ping(string s)
		{
			return s;
		}

		public ComplexModelResponse PingComplexModel(ComplexModelInput inputModel)
		{
			return new ComplexModelResponse
			{
				FloatProperty = float.MaxValue / 2,
				StringProperty = inputModel?.StringProperty,
				ListProperty = inputModel?.ListProperty,
				DateTimeOffsetProperty = inputModel?.DateTimeOffsetProperty ?? default,
				TestEnum = TestEnum.Two
			};
		}

		public int[] IntArray()
		{
			return new[] { 123, 456, 789 };
		}

		public void VoidMethod(out string s)
		{
			s = "Value from server";
		}

		public Task<int> AsyncMethod()
		{
			return Task.FromResult(42);
		}

		public int? NullableMethod(bool? arg)
		{
			return arg.HasValue ? (arg.Value ? 1 : 0) : (int?)null;
		}

		public void XmlMethod(XElement xml)
		{
			// Nothing to do: the sample only shows that an XElement parameter can be sent and received.
		}

		public Models.ComplexReturnModel[] ComplexReturnModel()
		{
			return new[]
			{
				new Models.ComplexReturnModel { Id = 1, Name = "Item 1" },
				new Models.ComplexReturnModel { Id = 2, Name = "Item 2" }
			};
		}
	}
}
