using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = "MessageContractWithArrays", WrapperNamespace = "http://xmlelement-namespace/", IsWrapped = true)]
	public class MessageContractRequestWithArrays
	{
		[MessageHeader(Namespace = "http://xmlelement-namespace/")]
		public ComplexModel1 Header { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 0)]
		[XmlElement("ArrayWithoutContainers")]
		public ComplexModel1[] ArrayWithoutContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 1)]
		[XmlArray("ArrayWithContainers")]
		[XmlArrayItem("ComplexModel1")]
		public ComplexModel1[] ArrayWithContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 5)]
		[XmlElement("ArrayWithChoiceWithoutContainers11", typeof(ComplexModel1))]
		[XmlElement("ArrayWithChoiceWithoutContainers12", typeof(ComplexModel2))]
		public object[] ArrayWithChoiceWithoutContainers1 { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 5)]
		[XmlElement("ArrayWithChoiceWithoutContainers21", typeof(ComplexModel1))]
		[XmlElement("ArrayWithChoiceWithoutContainers22", typeof(ComplexModel2))]
		public object[] ArrayWithChoiceWithoutContainers2 { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 2)]
		[XmlElement("ObjectArrayWithoutContainers")]
		public ComplexObject[] ObjectArrayWithoutContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 3)]
		[XmlArray("ObjectArrayWithContainers")]
		[XmlArrayItem("ComplexObject")]
		public ComplexObject[] ObjectArrayWithContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 5)]
		[XmlElement("EmptyArrayWithChoiceWithoutContainers1", typeof(ComplexModel1))]
		[XmlElement("EmptyArrayWithChoiceWithoutContainers2", typeof(ComplexModel2))]
		public object[] EmptyArrayWithChoiceWithoutContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 4)]
		[XmlElement("EmptyArrayWithoutContainers")]
		public ComplexModel1[] EmptyArrayWithoutContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 5)]
		[XmlArray("EmptyArrayWithContainers")]
		[XmlArrayItem("ComplexModel1")]
		public ComplexModel1[] EmptyArrayWithContainers { get; set; }

		public static MessageContractRequestWithArrays CreateSample()
			=> new MessageContractRequestWithArrays
			{
				Header = ComplexModel1.CreateSample1(),
				ArrayWithoutContainers = new[]
				{
					ComplexModel1.CreateSample1(),
					ComplexModel1.CreateSample2(),
					ComplexModel1.CreateSample3()
				},
				ArrayWithContainers = new[]
				{
					ComplexModel1.CreateSample1(),
					ComplexModel1.CreateSample2(),
					ComplexModel1.CreateSample3()
				},
				ArrayWithChoiceWithoutContainers1 = new[]
				{
					ComplexModel1.CreateSample1(),
					ComplexModel1.CreateSample2(),
					ComplexModel1.CreateSample3()
				},
				ArrayWithChoiceWithoutContainers2 = new[]
				{
					ComplexModel2.CreateSample1(),
					ComplexModel2.CreateSample2(),
					ComplexModel2.CreateSample3()
				},
				ObjectArrayWithoutContainers = new[]
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2(),
					ComplexObject.CreateSample3(),
				},
				ObjectArrayWithContainers = new[]
				{
					ComplexObject.CreateSample1(),
					ComplexObject.CreateSample2(),
					ComplexObject.CreateSample3(),
				},
				EmptyArrayWithChoiceWithoutContainers = new object[0],
				EmptyArrayWithoutContainers = new ComplexModel1[0],
				EmptyArrayWithContainers = new ComplexModel1[0]
			};
	}
}
