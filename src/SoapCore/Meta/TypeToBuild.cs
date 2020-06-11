using System;
using System.Reflection;
using System.Xml.Serialization;

namespace SoapCore.Meta
{
	public class TypeToBuild
	{
		public TypeToBuild(Type type)
		{
			Type = type;
			TypeName = type.GetSerializedTypeName();
			AnonymousTypeRootElementName = null;
			IsAnonumous = type.GetCustomAttribute<XmlTypeAttribute>()?.AnonymousType == true;
		}

		public bool IsAnonumous { get; }
		public Type Type { get; }
		public string TypeName { get; set; }
		public string AnonymousTypeRootElementName { get; set; }
	}
}
