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
			ref ComplexModelResponse responseModelRef,
			ComplexObject data1,
			out ComplexModelResponse responseModelOut,
			ComplexObject data2)
		{
			Console.WriteLine("input params:\n");
			Console.WriteLine($"{nameof(inputModel)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(inputModel)}\n");
			Console.WriteLine($"{nameof(responseModelRef)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef)}\n");
			Console.WriteLine($"{nameof(data1)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(data1)}\n");
			Console.WriteLine($"{nameof(data2)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(data2)}\n");

			responseModelRef = ComplexModelResponse.CreateSample2();
			responseModelOut = ComplexModelResponse.CreateSample3();

			Console.WriteLine("output params:\n");
			Console.WriteLine($"{nameof(responseModelRef)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef)}\n");
			Console.WriteLine($"{nameof(responseModelOut)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelOut)}\n");

			Console.WriteLine("done.\n");

			return true;
		}

		public bool PingComplexModelOutAndRef(
			ComplexModelInput inputModel,
			ref ComplexModelResponse responseModelRef,
			ComplexObject data1,
			out ComplexModelResponse responseModelOut,
			ComplexObject data2)
		{
			Console.WriteLine($"{nameof(PingComplexModelOutAndRef)}:");
			return PingComplexModelOutAndRefImplementation(inputModel, ref responseModelRef, data1, out responseModelOut, data2);
		}

		public PingComplexModelOldStyleResponse PingComplexModelOldStyle(
			PingComplexModelOldStyleRequest request)
		{
			Console.WriteLine($"{nameof(PingComplexModelOldStyle)}:");
			var response = new PingComplexModelOldStyleResponse();
			var result = PingComplexModelOutAndRefImplementation(request.inputModel, ref request.responseModelRef, request.data1, out response.responseModelOut, request.data2);
			response.responseModelRef = request.responseModelRef;
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
