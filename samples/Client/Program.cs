using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Server;

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
				IntProperty = int.MaxValue / 2
			};
			var complexResult = serviceClient.PingComplexModel(complexModel);
			Console.WriteLine("PingComplexModel result. FloatProperty: {0}, StringProperty: {1}", complexResult.FloatProperty, complexResult.StringProperty);
			Console.ReadKey();
		}
	}
}
