using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server
{
	public class SampleService : ISampleService
	{
		public SampleService()
		{
			Newtonsoft.Json.JsonConvert.DefaultSettings = (() =>
			{
				var settings = new Newtonsoft.Json.JsonSerializerSettings();
				settings.Formatting = Newtonsoft.Json.Formatting.Indented;
				settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter { CamelCaseText = true });
				return settings;
			});
		}

		public string Ping(string s)
		{
			Console.WriteLine("Exec ping method");
			return s;
		}

		public ComplexModelResponse PingComplexModel(ComplexModelInput inputModel)
		{
			Console.WriteLine("Input data. IntProperty: {0}, StringProperty: {1}", inputModel.IntProperty, inputModel.StringProperty);

			return new ComplexModelResponse
			{
				FloatProperty = float.MaxValue / 2,
				StringProperty = inputModel.StringProperty,
				ListProperty = inputModel.ListProperty,
				EnumProperty = SampleEnum.C
				//DateTimeOffsetProperty = inputModel.DateTimeOffsetProperty
			};
		}

		private bool PingComplexModelOutAndRefImplementation(
			ComplexModelInput inputModel,
			ref ComplexModelResponse responseModelRef1,
			ComplexObject data1,
			ref ComplexModelResponse responseModelRef2,
			ComplexObject data2,
			out ComplexModelResponse responseModelOut1,
			out ComplexModelResponse responseModelOut2)
		{
			Console.WriteLine("input params:\n");
			Console.WriteLine($"{nameof(inputModel)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(inputModel)}\n");
			Console.WriteLine($"{nameof(responseModelRef1)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef1)}\n");
			Console.WriteLine($"{nameof(responseModelRef2)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef2)}\n");
			Console.WriteLine($"{nameof(data1)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(data1)}\n");
			Console.WriteLine($"{nameof(data2)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(data2)}\n");

			responseModelRef1 = ComplexModelResponse.CreateSample2();
			responseModelRef2 = ComplexModelResponse.CreateSample1();
			responseModelOut1 = ComplexModelResponse.CreateSample3();
			responseModelOut2 = ComplexModelResponse.CreateSample1();

			Console.WriteLine("output params:\n");
			Console.WriteLine($"{nameof(responseModelRef1)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef1)}\n");
			Console.WriteLine($"{nameof(responseModelRef2)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef2)}\n");
			Console.WriteLine($"{nameof(responseModelOut1)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelOut1)}\n");
			Console.WriteLine($"{nameof(responseModelOut2)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelOut2)}\n");

			Console.WriteLine("done.\n");

			return true;
		}

		public bool PingComplexModelOutAndRef(
			ComplexModelInput inputModel,
			ref ComplexModelResponse responseModelRef1,
			ComplexObject data1,
			ref ComplexModelResponse responseModelRef2,
			ComplexObject data2,
			out ComplexModelResponse responseModelOut1,
			out ComplexModelResponse responseModelOut2)
		{
			Console.WriteLine($"{nameof(PingComplexModelOutAndRef)}:");
			return PingComplexModelOutAndRefImplementation(
				inputModel,
				ref responseModelRef1,
				data1,
				ref responseModelRef2,
				data2,
				out responseModelOut1,
				out responseModelOut2);
		}

		public PingComplexModelOldStyleResponse PingComplexModelOldStyle(
			PingComplexModelOldStyleRequest request)
		{
			Console.WriteLine($"{nameof(PingComplexModelOldStyle)}:");
			var response = new PingComplexModelOldStyleResponse();
			var result = PingComplexModelOutAndRefImplementation(
				request.inputModel,
				ref request.responseModelRef1,
				request.data1,
				ref request.responseModelRef2,
				request.data2,
				out response.responseModelOut1,
				out response.responseModelOut2);
			response.responseModelRef1 = request.responseModelRef1;
			response.responseModelRef2 = request.responseModelRef2;
			return response;
		}


		public bool EnumMethod(out SampleEnum e)
		{
			e = SampleEnum.B;
			return true;
		}

		public void VoidMethod(out string s)
		{
			s = "Value from server";
		}

		public Task<int> AsyncMethod()
		{
			return Task.Run(() => 42);
		}

		public int? NullableMethod(bool? arg)
		{
			return null;
		}
	}
}
