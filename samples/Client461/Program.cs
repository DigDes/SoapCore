using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        public static void Main(string[] args)
        {
            var binding = new BasicHttpBinding();
            var endpoint = new EndpointAddress(new Uri(string.Format("http://{0}:5050/Service.asmx", Environment.MachineName)));
            var serviceClient = new SampleService.SampleServiceClient(binding, endpoint);
            var result = serviceClient.Ping("hey");
            Console.WriteLine("Ping method result: {0}", result);

			// TODO: DateTimeOffsetProperty not working
			//var complexModel = new SampleService.ComplexModelInput
			//{
			//	StringProperty = Guid.NewGuid().ToString(),
			//	IntProperty = int.MaxValue / 2,
			//	ListProperty = new List<string> { "test", "list", "of", "strings" },
			//	DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			//};

			//var complexResult = serviceClient.PingComplexModel(complexModel);
			//Console.WriteLine("PingComplexModel result. FloatProperty: {0}, StringProperty: {1}, ListProperty: {2}, DateTimeOffsetProperty: {3}",
			//	complexResult.FloatProperty, complexResult.StringProperty, string.Join(", ", complexResult.ListProperty), complexResult.DateTimeOffsetProperty);

			var stringValue = serviceClient.VoidMethod();
			Console.WriteLine("Void method result: {0}", stringValue);

            var asyncMethodResult = serviceClient.AsyncMethodAsync().Result;
            Console.WriteLine("Async method result: {0}", asyncMethodResult);

            Console.ReadKey();
        }
    }
}
