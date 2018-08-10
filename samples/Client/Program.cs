using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Models;

namespace Client
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Newtonsoft.Json.JsonConvert.DefaultSettings = (() =>
			{
				var settings = new Newtonsoft.Json.JsonSerializerSettings();
				settings.Formatting = Newtonsoft.Json.Formatting.Indented;
				settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter { CamelCaseText = true });
				return settings;
			});

			var binding = new BasicHttpBinding();
			// todo: why DataContractSerializer not working?
			var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5050/Service.svc", Environment.MachineName)));
			var channelFactory = new ChannelFactory<ISampleService>(binding, endpoint);
			var serviceClient = channelFactory.CreateChannel();
			var result = serviceClient.Ping("hey");
			Console.WriteLine("Ping method result: {0}", result);

			var complexModel = new ComplexModelInput
			{
				StringProperty = Guid.NewGuid().ToString(),
				IntProperty = int.MaxValue / 2,
				ListProperty = new List<string> { "test", "list", "of", "strings" },
				//DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var pingComplexModelResult = serviceClient.PingComplexModel(complexModel);
			Console.WriteLine($"{nameof(pingComplexModelResult)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(pingComplexModelResult)}\n");

			serviceClient.EnumMethod(out var enumValue);
			Console.WriteLine("Enum method result: {0}", enumValue);

			var responseModelRef1 = ComplexModelResponse.CreateSample1();
			var responseModelRef2 = ComplexModelResponse.CreateSample2();
			var pingComplexModelOutAndRefResult =
				serviceClient.PingComplexModelOutAndRef(
					ComplexModelInput.CreateSample1(),
					ref responseModelRef1,
					ComplexObject.CreateSample1(),
					ref responseModelRef2,
					ComplexObject.CreateSample2(),
					out var responseModelOut1,
					out var responseModelOut2);
			Console.WriteLine($"{nameof(pingComplexModelOutAndRefResult)}: {pingComplexModelOutAndRefResult}\n");
			Console.WriteLine($"{nameof(responseModelRef1)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef1)}\n");
			Console.WriteLine($"{nameof(responseModelRef2)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelRef2)}\n");
			Console.WriteLine($"{nameof(responseModelOut1)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelOut1)}\n");
			Console.WriteLine($"{nameof(responseModelOut2)}:\n{Newtonsoft.Json.JsonConvert.SerializeObject(responseModelOut2)}\n");

			serviceClient.VoidMethod(out var stringValue);
			Console.WriteLine("Void method result: {0}", stringValue);

			var asyncMethodResult = serviceClient.AsyncMethod().Result;
			Console.WriteLine("Async method result: {0}", asyncMethodResult);

			Console.ReadKey();
		}
	}
}
