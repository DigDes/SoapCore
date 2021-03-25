using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Xml.Serialization;

namespace SoapCore.Tests.Serialization.Models.Xml
{
	[MessageContract(WrapperName = "MessageContractResponseWithArrays", WrapperNamespace = "http://xmlelement-namespace/", IsWrapped = true)]
	public class MessageContractResponseWithArrays
	{
		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 0)]
		[XmlElement("ArrayWithoutContainers")]
		public ComplexModel1[] ArrayWithoutContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 1)]
		[XmlArray("ArrayWithContainers")]
		[XmlArrayItem("ComplexModel1")]
		public ComplexModel1[] ArrayWithContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 2)]
		[XmlElement("ObjectArrayWithoutContainers")]
		public ComplexObject[] ObjectArrayWithoutContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 3)]
		[XmlArray("ObjectArrayWithContainers")]
		[XmlArrayItem("ComplexObject")]
		public ComplexObject[] ObjectArrayWithContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 4)]
		[XmlElement("EmptyArrayWithoutContainers")]
		public ComplexModel1[] EmptyArrayWithoutContainers { get; set; }

		[MessageBodyMember(Namespace = "http://xmlelement-namespace/", Order = 5)]
		[XmlArray("EmptyArrayWithContainers")]
		[XmlArrayItem("ComplexModel1")]
		public ComplexModel1[] EmptyArrayWithContainers { get; set; }

		public static MessageContractResponseWithArrays CreateSample()
			=> new MessageContractResponseWithArrays
			{
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
				EmptyArrayWithoutContainers = new ComplexModel1[0],
				EmptyArrayWithContainers = new ComplexModel1[0]
			};
	}
}
