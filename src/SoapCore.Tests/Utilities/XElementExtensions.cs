using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.Utilities
{
	public static class XElementExtensions
	{
		public static T DeserializeInnerElementAs<T>(this XElement element)
		{
			var serializer = new DataContractSerializer(typeof(FaultDetail));
			var innerElement = element.Elements().SingleOrDefault();

			using (var reader = innerElement.CreateReader())
			{
				return (T)serializer.ReadObject(reader);
			}
		}
	}
}
