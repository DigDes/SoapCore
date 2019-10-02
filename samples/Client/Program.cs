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
			var binding = new BasicHttpBinding();
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
				DateTimeOffsetProperty = new DateTimeOffset(2018, 12, 31, 13, 59, 59, TimeSpan.FromHours(1))
			};

			var complexResult = serviceClient.PingComplexModel(complexModel);
			Console.WriteLine("PingComplexModel result. FloatProperty: {0}, StringProperty: {1}, ListProperty: {2}, DateTimeOffsetProperty: {3}, EnumProperty: {4}",
				complexResult.FloatProperty, complexResult.StringProperty, string.Join(", ", complexResult.ListProperty), complexResult.DateTimeOffsetProperty, complexResult.TestEnum);

			serviceClient.VoidMethod(out var stringValue);
			Console.WriteLine("Void method result: {0}", stringValue);

			var asyncMethodResult = serviceClient.AsyncMethod().Result;
			Console.WriteLine("Async method result: {0}", asyncMethodResult);

			var xmlelement = System.Xml.Linq.XElement.Parse("<test>string</test>");
			serviceClient.XmlMethod(xmlelement);

			Console.ReadKey();
		}
	}
}
