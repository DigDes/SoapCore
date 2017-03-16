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
			Console.WriteLine(result);
			Console.ReadKey();
		}
	}
}
